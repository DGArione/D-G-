namespace DIG.Core.Models;

/// <summary>An infrastructure inventory record. Credentials are never stored here —
/// only a reference to a secret (secret_ref) kept in the secure store.</summary>
public sealed record InfrastructureAsset(
    long Id,
    string Name,
    string? Ip,
    string? Fqdn,
    int? Port,
    string? Environment,
    string? Customer,
    string? Product,
    string? Version,
    string? Role,
    string? Tags,
    string? Notes,
    string? Connection,
    string? UsernameRef,
    DateTimeOffset UpdatedUtc)
{
    public static InfrastructureAsset Empty => new(
        0, "", null, null, null, null, null, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
}
