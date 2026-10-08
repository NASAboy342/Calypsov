namespace Calypsov.Models;

/// <summary>A snapshot of an in-progress (or just-finished) encrypt/decrypt operation.</summary>
/// <param name="Threads">
/// Per-worker breakdown while Turbo Zip is splitting a category across multiple zip parts;
/// empty when running single-threaded (Turbo Zip off, or below its folder-count threshold).
/// </param>
public sealed record EncryptionProgress(
    bool IsRunning,
    int Completed,
    int Total,
    string? CurrentItem,
    IReadOnlyList<ZipThreadProgress> Threads);
