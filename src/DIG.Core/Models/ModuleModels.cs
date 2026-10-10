namespace DIG.Core.Models;

public sealed record TaskItem(
    long Id, string Title, bool Done, string Priority, DateTimeOffset? DueUtc, string? Tags, DateTimeOffset UpdatedUtc);

public sealed record NoteItem(
    long Id, string Title, string Body, string? Tags, DateTimeOffset UpdatedUtc);

public sealed record CalendarEvent(
    long Id, string Title, DateTimeOffset StartUtc, DateTimeOffset EndUtc, string Kind, string? Color, string? Notes)
{
    public string When => StartUtc.ToLocalTime().ToString("ddd dd MMM, HH:mm") + " – " + EndUtc.ToLocalTime().ToString("HH:mm");
}

/// <summary>A saved remote-access target (SSH / RDP / SFTP).</summary>
public sealed record SshProfile(
    long Id, string Name, string Host, int Port, string? Username, string Kind, string? Notes, DateTimeOffset UpdatedUtc)
{
    public string Target => string.IsNullOrWhiteSpace(Username) ? $"{Host}:{Port}" : $"{Username}@{Host}:{Port}";
}

public sealed record KbEntry(
    long Id, string Title, string Content, string? Source, string? Tags, DateTimeOffset UpdatedUtc);
