namespace Calypsov.Models;

/// <summary>A snapshot of one Turbo Zip worker's progress through its own chunk of folders.</summary>
public sealed record ZipThreadProgress(int ThreadIndex, int Completed, int Total, string? CurrentItem);
