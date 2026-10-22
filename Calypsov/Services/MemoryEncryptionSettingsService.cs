using Calypsov.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Calypsov.Services;

/// <inheritdoc cref="IEncryptionSettingsService"/>
public sealed class MemoryEncryptionSettingsService : IEncryptionSettingsService
{
    private const string ZipSettingsCacheKey = "encryption:zipSettings";
    private const string IsRecordLogCacheKey = "encryption:isRecordLog";
    private const string IsEncryptAppSettingCacheKey = "encryption:isEncryptAppSetting";
    private const string ProfilesCacheKey = "encryption:profiles";
    private const string CurrentProfileIdCacheKey = "encryption:currentProfileId";

    private const string DefaultProfileName = "Default";
    private const string DefaultProfileColor = "teal";
    private const string DefaultProfileIcon = "shield";

    /// <summary>Shown (and used as the thrown <see cref="InvalidOperationException"/>'s message)
    /// whenever a setting is changed while files are protected.</summary>
    private const string SettingsLockedMessage = "Setting cannot be change while files are protected. Turn off the protection first before change any setting.";

    private readonly IMemoryCache _cache;
    private readonly object _lock = new();

    public MemoryEncryptionSettingsService(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>Lazily seeds a single "Default" profile the first time any profile-aware method is
    /// called, mirroring <see cref="StorageEncryptionSettingsService"/>'s migration-creates-a-
    /// Default-profile behavior for a brand new install.</summary>
    private List<ProtectionProfile> GetProfilesUnsafe()
    {
        var profiles = _cache.Get<List<ProtectionProfile>>(ProfilesCacheKey);
        if (profiles == null || profiles.Count == 0)
        {
            profiles = new List<ProtectionProfile>
            {
                new() { Id = Guid.NewGuid(), Name = DefaultProfileName, Color = DefaultProfileColor, Icon = DefaultProfileIcon },
            };
            _cache.Set(ProfilesCacheKey, profiles);
            _cache.Set(CurrentProfileIdCacheKey, profiles[0].Id);
        }
        return profiles;
    }

    private ProtectionProfile GetCurrentProfileUnsafe()
    {
        var profiles = GetProfilesUnsafe();
        var currentId = _cache.Get<Guid?>(CurrentProfileIdCacheKey) ?? profiles[0].Id;
        return profiles.FirstOrDefault(p => p.Id == currentId) ?? profiles[0];
    }

    /// <summary>Throws <see cref="InvalidOperationException"/> if files are already encrypted. This
    /// stand-in never has a real encrypt/decrypt run in progress (see <see cref="GetProgress"/>), so
    /// unlike <see cref="StorageEncryptionSettingsService"/> there's no separate in-progress check.</summary>
    private void EnsureSettingsAreChangeable()
    {
        if (IsEncryptionEnabled())
        {
            throw new InvalidOperationException(SettingsLockedMessage);
        }
    }

    public bool IsEncryptionEnabled()
    {
        lock (_lock)
        {
            return GetCurrentProfileUnsafe().IsEncrypted;
        }
    }

    public bool SetEncryptionEnabled(bool enabled)
    {
        lock (_lock)
        {
            GetCurrentProfileUnsafe().IsEncrypted = enabled;
            return enabled;
        }
    }

    public bool ToggleEncryption()
    {
        lock (_lock)
        {
            var profile = GetCurrentProfileUnsafe();
            profile.IsEncrypted = !profile.IsEncrypted;
            return profile.IsEncrypted;
        }
    }

    public ZipSettings GetZipSettings() =>
        _cache.Get<ZipSettings?>(ZipSettingsCacheKey) ?? new ZipSettings(false, false);

    public ZipSettings SetZipSettings(bool isUseTurboZip, bool isEncryptZippedFile)
    {
        EnsureSettingsAreChangeable();
        lock (_lock)
        {
            var settings = new ZipSettings(isUseTurboZip, isEncryptZippedFile);
            _cache.Set(ZipSettingsCacheKey, settings);
            return settings;
        }
    }

    public bool GetIsRecordLogEnabled() => _cache.Get<bool?>(IsRecordLogCacheKey) ?? false;

    public bool SetIsRecordLogEnabled(bool enabled)
    {
        EnsureSettingsAreChangeable();
        lock (_lock)
        {
            _cache.Set(IsRecordLogCacheKey, enabled);
            return enabled;
        }
    }

    /// <summary>This stand-in never touches a real file, so there's nothing to encode — the flag is
    /// still stored (defaulting to true, matching <see cref="Models.AppSetting"/>) for interface
    /// parity with <see cref="StorageEncryptionSettingsService"/>.</summary>
    public bool GetIsEncryptAppSettingEnabled() => _cache.Get<bool?>(IsEncryptAppSettingCacheKey) ?? true;

    public bool SetIsEncryptAppSettingEnabled(bool enabled)
    {
        EnsureSettingsAreChangeable();
        lock (_lock)
        {
            _cache.Set(IsEncryptAppSettingCacheKey, enabled);
            return enabled;
        }
    }

    /// <summary>This stand-in doesn't do any real zip/unzip work, so there's never anything running.</summary>
    public EncryptionProgress GetProgress() =>
        new(IsRunning: false, Completed: 0, Total: 0, CurrentItem: null, Threads: Array.Empty<ZipThreadProgress>());

    public void CancelEncryption() =>
        throw new InvalidOperationException("There is no encryption in progress to cancel.");

    public IReadOnlyList<EncryptionTarget> GetTargets()
    {
        lock (_lock)
        {
            return GetCurrentProfileUnsafe().EncryptionTargets.ToList();
        }
    }

    public EncryptionTarget AddTarget(EnumTargetCategory category, string path)
    {
        EnsureSettingsAreChangeable();
        var trimmedPath = path.Trim();
        if (trimmedPath.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        lock (_lock)
        {
            var profile = GetCurrentProfileUnsafe();

            var isDuplicate = profile.EncryptionTargets.Any(t => t.Category == category && t.Path == trimmedPath);
            if (isDuplicate)
            {
                throw new InvalidOperationException("That path has already been added.");
            }

            var target = new EncryptionTarget(Guid.NewGuid(), category, trimmedPath);
            profile.EncryptionTargets.Add(target);
            return target;
        }
    }

    public bool RemoveTarget(Guid id)
    {
        EnsureSettingsAreChangeable();
        lock (_lock)
        {
            return GetCurrentProfileUnsafe().EncryptionTargets.RemoveAll(t => t.Id == id) > 0;
        }
    }

    public string? GetSelectedBrowserProfileId(BrowserType browser)
    {
        lock (_lock)
        {
            return GetCurrentProfileUnsafe().BrowserProfileSelections.TryGetValue(browser, out var id) ? id : null;
        }
    }

    public string? SetSelectedBrowserProfileId(BrowserType browser, string? profileId)
    {
        EnsureSettingsAreChangeable();
        lock (_lock)
        {
            GetCurrentProfileUnsafe().BrowserProfileSelections[browser] = profileId;
            return profileId;
        }
    }

    // --- Protection profiles ---

    public IReadOnlyList<ProtectionProfile> GetProfiles()
    {
        lock (_lock)
        {
            return GetProfilesUnsafe().ToList();
        }
    }

    public Guid GetCurrentProfileId()
    {
        lock (_lock)
        {
            return GetCurrentProfileUnsafe().Id;
        }
    }

    public ProtectionProfile CreateProfile(string name, string color, string icon)
    {
        ValidateProfileFields(name, color, icon);
        EnsureSettingsAreChangeable();
        lock (_lock)
        {
            var profiles = GetProfilesUnsafe();
            var profile = new ProtectionProfile { Id = Guid.NewGuid(), Name = name, Color = color, Icon = icon };
            profiles.Add(profile);
            _cache.Set(CurrentProfileIdCacheKey, profile.Id);
            return profile;
        }
    }

    public ProtectionProfile UpdateProfile(Guid profileId, string name, string color, string icon)
    {
        ValidateProfileFields(name, color, icon);
        lock (_lock)
        {
            var profile = FindProfileOrThrow(profileId);
            profile.Name = name;
            profile.Color = color;
            profile.Icon = icon;
            return profile;
        }
    }

    public ProtectionProfile SwitchProfile(Guid profileId)
    {
        EnsureSettingsAreChangeable();
        lock (_lock)
        {
            var profile = FindProfileOrThrow(profileId);
            _cache.Set(CurrentProfileIdCacheKey, profile.Id);
            return profile;
        }
    }

    public void DeleteProfile(Guid profileId)
    {
        EnsureSettingsAreChangeable();
        lock (_lock)
        {
            var profiles = GetProfilesUnsafe();
            var profile = FindProfileOrThrow(profileId);
            if (profiles.Count <= 1)
            {
                throw new ArgumentException("Cannot delete the only remaining profile.");
            }

            var currentId = _cache.Get<Guid?>(CurrentProfileIdCacheKey);
            profiles.Remove(profile);
            if (currentId == profile.Id)
            {
                _cache.Set(CurrentProfileIdCacheKey, profiles[0].Id);
            }
        }
    }

    private ProtectionProfile FindProfileOrThrow(Guid profileId)
    {
        var profile = GetProfilesUnsafe().FirstOrDefault(p => p.Id == profileId);
        if (profile == null)
        {
            throw new ArgumentException($"No profile found with id \"{profileId}\".");
        }
        return profile;
    }

    private static void ValidateProfileFields(string name, string color, string icon)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name is required.");
        if (string.IsNullOrWhiteSpace(color))
            throw new ArgumentException("Profile color is required.");
        if (string.IsNullOrWhiteSpace(icon))
            throw new ArgumentException("Profile icon is required.");
    }
}
