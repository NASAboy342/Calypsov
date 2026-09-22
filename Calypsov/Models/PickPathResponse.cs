namespace Calypsov.Models;

/// <summary>Result of a native file/folder picker dialog. Null when the user cancelled.</summary>
public sealed record PickPathResponse(string? Path);
