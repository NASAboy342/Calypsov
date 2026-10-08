using Calypsov.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Calypsov.Services;

/// <inheritdoc cref="IEncryptionSettingsService"/>
public sealed class MemoryEncryptionSettingsService : IEncryptionSettingsService
{
    private const string EnabledCacheKey = "encryption:enabled";
    private const string TargetsCacheKey = "encryption:targets";
    private const string ZipSettingsCacheKey = "encryption:zipSettings";

    private readonly IMemoryCache _cache;
    private readonly object _lock = new();

    public MemoryEncryptionSettingsService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool IsEncryptionEnabled() => _cache.Get<bool?>(EnabledCacheKey) ?? false;

    public bool SetEncryptionEnabled(bool enabled)
    {
        lock (_lock)
        {
            _cache.Set(EnabledCacheKey, enabled);
            return enabled;
        }
    }

    public bool ToggleEncryption()
    {
        lock (_lock)
        {
            var next = !IsEncryptionEnabled();
            _cache.Set(EnabledCacheKey, next);
            return next;
        }
    }

    public ZipSettings GetZipSettings() =>
        _cache.Get<ZipSettings?>(ZipSettingsCacheKey) ?? new ZipSettings(false, false);

    public ZipSettings SetZipSettings(bool isUseTurboZip, bool isEncryptZippedFile)
    {
        lock (_lock)
        {
            var settings = new ZipSettings(isUseTurboZip, isEncryptZippedFile);
            _cache.Set(ZipSettingsCacheKey, settings);
            return settings;
        }
    }

    /// <summary>This stand-in doesn't do any real zip/unzip work, so there's never anything running.</summary>
    public EncryptionProgress GetProgress() =>
        new(IsRunning: false, Completed: 0, Total: 0, CurrentItem: null, Threads: Array.Empty<ZipThreadProgress>());

    public IReadOnlyList<EncryptionTarget> GetTargets()
    {
        lock (_lock)
        {
            return GetTargetsUnsafe().ToList();
        }
    }

    public EncryptionTarget AddTarget(EnumTargetCategory category, string path)
    {
        var trimmedPath = path.Trim();
        if (trimmedPath.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        lock (_lock)
        {
            var targets = GetTargetsUnsafe();

            var isDuplicate = targets.Any(t => t.Category == category && t.Path == trimmedPath);
            if (isDuplicate)
            {
                throw new InvalidOperationException("That path has already been added.");
            }

            var target = new EncryptionTarget(Guid.NewGuid(), category, trimmedPath);
            targets.Add(target);
            _cache.Set(TargetsCacheKey, targets);
            return target;
        }
    }

    public bool RemoveTarget(Guid id)
    {
        lock (_lock)
        {
            var targets = GetTargetsUnsafe();
            var removed = targets.RemoveAll(t => t.Id == id) > 0;
            if (removed)
            {
                _cache.Set(TargetsCacheKey, targets);
            }
            return removed;
        }
    }

    private List<EncryptionTarget> GetTargetsUnsafe() =>
        _cache.Get<List<EncryptionTarget>>(TargetsCacheKey) ?? [];
}
