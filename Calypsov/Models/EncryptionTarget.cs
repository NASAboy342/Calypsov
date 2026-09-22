namespace Calypsov.Models;

/// <summary>A folder or file the user has configured for encryption. Category is "folder" or "file".</summary>
public sealed record EncryptionTarget(Guid Id, string Category, string Path);
