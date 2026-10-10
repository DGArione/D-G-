using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DIG.Core.Security;

namespace DIG.App.Services;

/// <summary>
/// Windows implementation of <see cref="ISecretStore"/> backed by DPAPI
/// (per-user encryption). Secrets are encrypted with
/// <see cref="DataProtectionScope.CurrentUser"/> and written to a file that only
/// the current Windows user can decrypt — never to the SQLite DB, logs or config.
/// </summary>
public sealed class DpapiSecretStore : ISecretStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly byte[] _entropy = Encoding.UTF8.GetBytes("DIG.SecretStore.v1");

    public string BackendDescription => "Windows DPAPI (per-user encryption)";

    public DpapiSecretStore(string? filePath = null)
    {
        _path = filePath ?? Path.Combine(AppPaths.DataDir, "secrets.dat");
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    public void Set(string name, string secret)
    {
        lock (_gate)
        {
            var map = Load();
            map[name] = Protect(secret);
            Save(map);
        }
    }

    public string? Get(string name)
    {
        lock (_gate)
        {
            var map = Load();
            return map.TryGetValue(name, out var enc) ? Unprotect(enc) : null;
        }
    }

    public bool Exists(string name)
    {
        lock (_gate)
        {
            return Load().ContainsKey(name);
        }
    }

    public void Delete(string name)
    {
        lock (_gate)
        {
            var map = Load();
            if (map.Remove(name)) Save(map);
        }
    }

    private Dictionary<string, string> Load()
    {
        if (!File.Exists(_path)) return new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private void Save(Dictionary<string, string> map)
    {
        var json = JsonSerializer.Serialize(map);
        File.WriteAllText(_path, json);
    }

    private string Protect(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var enc = ProtectedData.Protect(bytes, _entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(enc);
    }

    private string? Unprotect(string base64)
    {
        try
        {
            var enc = Convert.FromBase64String(base64);
            var dec = ProtectedData.Unprotect(enc, _entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(dec);
        }
        catch
        {
            return null;
        }
    }
}
