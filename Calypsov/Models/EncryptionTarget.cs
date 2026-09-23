namespace Calypsov.Models;

/// <summary>A folder or file the user has configured for encryption.</summary>
public sealed record EncryptionTarget(Guid Id, EnumTargetCategory Category, string Path);
