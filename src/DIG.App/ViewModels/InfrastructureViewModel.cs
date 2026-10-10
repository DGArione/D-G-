using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.Core.Data;
using DIG.Core.Models;

namespace DIG.App.ViewModels;

public sealed partial class InfrastructureViewModel : ObservableObject
{
    private readonly InfrastructureRepository _repo;

    public event Action<string>? NavigateRequested;

    public InfrastructureViewModel(InfrastructureRepository repo)
    {
        _repo = repo;
        Assets = new ObservableCollection<InfrastructureAsset>();
        Reload();
    }

    public ObservableCollection<InfrastructureAsset> Assets { get; }
    public IReadOnlyList<string> Environments { get; } = new[]
    {
        "", "Production", "Staging", "Development", "Lab", "Customer", "DR"
    };

    [ObservableProperty] private string _searchTerm = string.Empty;
    [ObservableProperty] private InfrastructureAsset? _selectedAsset;
    [ObservableProperty] private string _status = string.Empty;

    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editIp = string.Empty;
    [ObservableProperty] private string _editFqdn = string.Empty;
    [ObservableProperty] private string _editPort = string.Empty;
    [ObservableProperty] private string _editEnvironment = string.Empty;
    [ObservableProperty] private string _editCustomer = string.Empty;
    [ObservableProperty] private string _editProduct = string.Empty;
    [ObservableProperty] private string _editVersion = string.Empty;
    [ObservableProperty] private string _editRole = string.Empty;
    [ObservableProperty] private string _editTags = string.Empty;
    [ObservableProperty] private string _editNotes = string.Empty;
    [ObservableProperty] private string _editConnection = string.Empty;
    [ObservableProperty] private string _editUsername = string.Empty;

    partial void OnSearchTermChanged(string value) => Reload();

    partial void OnSelectedAssetChanged(InfrastructureAsset? value)
    {
        var a = value;
        if (a is null) return;
        EditName = a.Name;
        EditIp = a.Ip ?? "";
        EditFqdn = a.Fqdn ?? "";
        EditPort = a.Port?.ToString() ?? "";
        EditEnvironment = a.Environment ?? "";
        EditCustomer = a.Customer ?? "";
        EditProduct = a.Product ?? "";
        EditVersion = a.Version ?? "";
        EditRole = a.Role ?? "";
        EditTags = a.Tags ?? "";
        EditNotes = a.Notes ?? "";
        EditConnection = a.Connection ?? "";
        EditUsername = a.UsernameRef ?? "";
    }

    private void Reload()
    {
        var items = _repo.Search(SearchTerm);
        Assets.Clear();
        foreach (var a in items) Assets.Add(a);
    }

    private InfrastructureAsset CurrentEdited()
    {
        int? port = int.TryParse(EditPort, out var p) ? p : null;
        string? N(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        return new InfrastructureAsset(
            SelectedAsset?.Id ?? 0,
            string.IsNullOrWhiteSpace(EditName) ? "Unnamed asset" : EditName.Trim(),
            N(EditIp), N(EditFqdn), port, N(EditEnvironment), N(EditCustomer), N(EditProduct),
            N(EditVersion), N(EditRole), N(EditTags), N(EditNotes), N(EditConnection), N(EditUsername),
            DateTimeOffset.UtcNow);
    }

    [RelayCommand]
    private void NewAsset()
    {
        SelectedAsset = null;
        EditName = "New asset";
        EditIp = EditFqdn = EditPort = EditEnvironment = EditCustomer = EditProduct =
            EditVersion = EditRole = EditTags = EditNotes = EditConnection = EditUsername = string.Empty;
        Status = "Editing a new asset. Fill the fields and Save.";
    }

    [RelayCommand]
    private void Save()
    {
        var id = _repo.Upsert(CurrentEdited());
        Reload();
        SelectedAsset = Assets.FirstOrDefault(a => a.Id == id);
        Status = "Asset saved.";
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedAsset is { Id: > 0 } a)
        {
            _repo.Delete(a.Id);
            Reload();
            SelectedAsset = null;
            Status = "Asset deleted.";
        }
    }

    [RelayCommand]
    private void OpenManagement()
    {
        var host = !string.IsNullOrWhiteSpace(EditFqdn) ? EditFqdn.Trim()
                 : !string.IsNullOrWhiteSpace(EditIp) ? EditIp.Trim()
                 : null;
        if (host is null) { Status = "No IP or FQDN to open."; return; }
        var url = "https://" + host;
        if (int.TryParse(EditPort, out var port) && port is > 0 and not 443 and not 80)
            url += ":" + port;
        NavigateRequested?.Invoke(url);
    }

    [RelayCommand]
    private void CopyIp() => Copy(EditIp);

    [RelayCommand]
    private void CopyFqdn() => Copy(EditFqdn);

    private void Copy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        try { System.Windows.Clipboard.SetText(value); Status = "Copied."; }
        catch { Status = "Clipboard unavailable."; }
    }
}
