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

    ZipSettings GetZipSettings();

    /// <summary>Throws <see cref="InvalidOperationException"/> if files are currently protected
    /// (already encrypted, or an encrypt/decrypt run is in progress) — settings can't be changed
    /// until protection is turned off.</summary>
    ZipSettings SetZipSettings(bool isUseTurboZip, bool isEncryptZippedFile);

    /// <summary>
    /// Backs <see cref="ILogService"/>'s IsRecordLog toggle. Lives here (alongside the other
    /// AppSetting-backed flags) rather than on a second service reading/writing the same
    /// settings file, so there's exactly one in-memory cache of AppSetting to stay consistent.
    /// </summary>
    bool GetIsRecordLogEnabled();

    /// <summary>Throws <see cref="InvalidOperationException"/> if files are currently protected
    /// (already encrypted, or an encrypt/decrypt run is in progress) — settings can't be changed
    /// until protection is turned off.</summary>
    bool SetIsRecordLogEnabled(bool enabled);

    /// <summary>
    /// Whether AppSetting (settings.json) is Base64-encoded at rest. Encoding/decoding is handled
    /// entirely by <see cref="Repositories.ISettingRepository"/> — this service only persists the
    /// flag's value and never encodes/decodes anything itself. Defaults to true. Reading an existing
    /// settings.json always works regardless of this flag's current value (the repository
    /// auto-detects whether the file is encoded); the flag only controls what happens on the next
    /// write.
    /// </summary>
    bool GetIsEncryptAppSettingEnabled();

    /// <summary>Throws <see cref="InvalidOperationException"/> if files are currently protected
    /// (already encrypted, or an encrypt/decrypt run is in progress) — settings can't be changed
    /// until protection is turned off.</summary>
    bool SetIsEncryptAppSettingEnabled(bool enabled);

    /// <summary>A snapshot of the current (or last) encrypt/decrypt run, safe to poll frequently
    /// while <see cref="ToggleEncryption"/> is in flight on another request.</summary>
    EncryptionProgress GetProgress();

    IReadOnlyList<EncryptionTarget> GetTargets();

    /// <summary>Throws <see cref="ArgumentException"/> for an invalid path, or
    /// <see cref="InvalidOperationException"/> if the path is already configured, or if files are
    /// currently protected (already encrypted, or an encrypt/decrypt run is in progress) —
    /// settings can't be changed until protection is turned off.</summary>
    EncryptionTarget AddTarget(EnumTargetCategory category, string path);

    /// <summary>Throws <see cref="InvalidOperationException"/> if files are currently protected
    /// (already encrypted, or an encrypt/decrypt run is in progress) — settings can't be changed
    /// until protection is turned off.</summary>
    bool RemoveTarget(Guid id);
}
