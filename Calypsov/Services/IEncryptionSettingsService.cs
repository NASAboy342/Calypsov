using Calypsov.Models;

namespace Calypsov.Services;

/// <summary>
/// Holds encryption on/off state and the configured folder/file targets, partitioned into named,
/// switchable <see cref="ProtectionProfile"/>s. Every method below that reads/writes targets, browser
/// profile selections, or encryption state operates on whichever profile is currently active
/// (<see cref="GetCurrentProfileId"/>) — see the profile-management methods at the bottom for
/// creating/switching/deleting/renaming profiles themselves.
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

    /// <summary>Cancels an encrypt run that is still zipping: the zip files are removed, nothing is
    /// deleted from its original location, and the in-flight <see cref="ToggleEncryption"/> returns
    /// <c>false</c> (still unprotected). Throws <see cref="InvalidOperationException"/> if there is
    /// nothing to cancel — no run in progress, a restore (unzip) run, or the run has already moved on
    /// to deleting the originals.</summary>
    void CancelEncryption();

    /// <summary>The current profile's configured targets.</summary>
    IReadOnlyList<EncryptionTarget> GetTargets();

    /// <summary>Throws <see cref="ArgumentException"/> for an invalid path, or
    /// <see cref="InvalidOperationException"/> if the path is already configured, or if files are
    /// currently protected (already encrypted, or an encrypt/decrypt run is in progress) —
    /// settings can't be changed until protection is turned off. Adds to the current profile.</summary>
    EncryptionTarget AddTarget(EnumTargetCategory category, string path);

    /// <summary>Throws <see cref="InvalidOperationException"/> if files are currently protected
    /// (already encrypted, or an encrypt/decrypt run is in progress) — settings can't be changed
    /// until protection is turned off. Removes from the current profile.</summary>
    bool RemoveTarget(Guid id);

    /// <summary>The browser profile id currently selected for <paramref name="browser"/> on the
    /// current protection profile, or null for "None".</summary>
    string? GetSelectedBrowserProfileId(BrowserType browser);

    /// <summary>Throws <see cref="InvalidOperationException"/> if files are currently protected
    /// (already encrypted, or an encrypt/decrypt run is in progress) — same guard as targets, since
    /// a selected browser profile's folder is encrypted alongside them. Sets the selection on the
    /// current protection profile; null selects "None".</summary>
    string? SetSelectedBrowserProfileId(BrowserType browser, string? profileId);

    // --- Protection profiles ---

    /// <summary>All configured protection profiles, in creation order.</summary>
    IReadOnlyList<ProtectionProfile> GetProfiles();

    /// <summary>The id of the profile whose targets/browser-profile-selections/encryption state are
    /// currently active.</summary>
    Guid GetCurrentProfileId();

    /// <summary>Throws <see cref="ArgumentException"/> if name, color, or icon is empty, or
    /// <see cref="InvalidOperationException"/> if files are currently protected. Creates a new,
    /// empty profile (no targets, no browser-profile selections) and switches to it immediately.</summary>
    ProtectionProfile CreateProfile(string name, string color, string icon);

    /// <summary>Throws <see cref="ArgumentException"/> if profileId doesn't match an existing
    /// profile, or if name/color/icon is empty. Not gated by the "protected" guard — renaming/
    /// recoloring/re-iconing a profile doesn't touch what's actually protected.</summary>
    ProtectionProfile UpdateProfile(Guid profileId, string name, string color, string icon);

    /// <summary>Throws <see cref="ArgumentException"/> if profileId doesn't match an existing
    /// profile, or <see cref="InvalidOperationException"/> if files are currently protected.</summary>
    ProtectionProfile SwitchProfile(Guid profileId);

    /// <summary>Throws <see cref="ArgumentException"/> if profileId doesn't match an existing
    /// profile, or if it's the only remaining profile (there must always be at least one), or
    /// <see cref="InvalidOperationException"/> if files are currently protected. If the deleted
    /// profile was the current one, the first remaining profile (in list order) automatically
    /// becomes current.</summary>
    void DeleteProfile(Guid profileId);
}
