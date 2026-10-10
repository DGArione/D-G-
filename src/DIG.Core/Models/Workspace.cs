namespace DIG.Core.Models;

/// <summary>A named set of saved browser tab URLs the user can reopen together.</summary>
public sealed record Workspace(long Id, string Name, IReadOnlyList<string> Urls, DateTimeOffset UpdatedUtc)
{
    public int TabCount => Urls.Count;
    public string Summary => Urls.Count == 1 ? "1 tab" : $"{Urls.Count} tabs";
}
