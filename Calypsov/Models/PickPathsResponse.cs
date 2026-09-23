namespace Calypsov.Models;

/// <summary>Paths chosen in a native file/folder picker dialog. Empty when the user cancelled.</summary>
public sealed record PickPathsResponse(string[] Paths);
