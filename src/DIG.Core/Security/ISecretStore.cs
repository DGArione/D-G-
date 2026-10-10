using System.Collections.Concurrent;

namespace DIG.Core.Security;

/// <summary>
/// Abstraction over secret storage. The real Windows implementation (DPAPI /
/// Credential Manager) lives in the app layer; secrets are never written to the
/// SQLite database, config files, logs, bookmarks or AI prompts.
/// </summary>
public interface ISecretStore
{
    /// <summary>Stores (or replaces) a secret under a logical name.</summary>
    void Set(string name, string secret);

    /// <summary>Returns the secret, or null if it does not exist / cannot be read.</summary>
    string? Get(string name);

    bool Exists(string name);

    void Delete(string name);

    /// <summary>Human-readable description of where secrets are kept (for the UI).</summary>
    string BackendDescription { get; }
}

/// <summary>
/// Non-persistent fallback used for tests and non-Windows hosts. Secrets live
/// only in memory for the process lifetime and are never written to disk.
/// </summary>
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _store = new(StringComparer.Ordinal);

    public string BackendDescription => "In-memory (non-persistent) — not encrypted at rest";

    public void Set(string name, string secret) => _store[name] = secret;

    public string? Get(string name) => _store.TryGetValue(name, out var v) ? v : null;

    public bool Exists(string name) => _store.ContainsKey(name);

    public void Delete(string name) => _store.TryRemove(name, out _);
}
