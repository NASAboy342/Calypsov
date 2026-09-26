namespace Calypsov.Models;

/// <summary>A browser profile detected on the user's machine.</summary>
public sealed record BrowserProfile(string Id, string Name, string? AvatarUrl);
