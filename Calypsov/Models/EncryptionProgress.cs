namespace Calypsov.Models;

/// <summary>A snapshot of an in-progress (or just-finished) encrypt/decrypt operation.</summary>
public sealed record EncryptionProgress(bool IsRunning, int Completed, int Total, string? CurrentItem);
