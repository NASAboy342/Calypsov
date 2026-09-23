using Calypsov.Models;

namespace Calypsov.Services;

/// <summary>
/// Holds encryption on/off state and the configured folder/file targets.
/// This is a state store only — the actual encrypt/decrypt behavior is not implemented yet.
/// </summary>
public interface IEncryptionSettingsService
{
    bool IsEncryptionEnabled();
    bool SetEncryptionEnabled(bool enabled);
    bool ToggleEncryption();

    IReadOnlyList<EncryptionTarget> GetTargets();

    /// <summary>Throws <see cref="ArgumentException"/> for an invalid path, or
    /// <see cref="InvalidOperationException"/> if the path is already configured.</summary>
    EncryptionTarget AddTarget(EnumTargetCategory category, string path);

    bool RemoveTarget(Guid id);
}
