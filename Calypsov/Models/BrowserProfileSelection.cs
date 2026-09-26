namespace Calypsov.Models;

/// <summary>The profile currently selected for a browser, or null for "None" (the default).</summary>
public sealed record BrowserProfileSelection(string? ProfileId);
