using Calypsov.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Calypsov.Services;

/// <inheritdoc cref="IEncryptionSettingsService"/>
public sealed class EncryptionSettingsService : IEncryptionSettingsService
{
    private const string EnabledCacheKey = "encryption:enabled";
    private const string TargetsCacheKey = "encryption:targets";

    private static readonly string[] AllowedCategories = ["folder", "file"];

    private readonly IMemoryCache _cache;
    private readonly object _lock = new();

    public EncryptionSettingsService(IMemoryCache cache)
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

    public IReadOnlyList<EncryptionTarget> GetTargets()
    {
        lock (_lock)
        {
            return GetTargetsUnsafe().ToList();
        }
    }

    public EncryptionTarget AddTarget(string category, string path)
    {
        var normalizedCategory = category.Trim().ToLowerInvariant();
        if (!AllowedCategories.Contains(normalizedCategory))
        {
            throw new ArgumentException("Category must be \"folder\" or \"file\".", nameof(category));
        }

        var trimmedPath = path.Trim();
        if (trimmedPath.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        lock (_lock)
        {
            var targets = GetTargetsUnsafe();

            var isDuplicate = targets.Any(t => t.Category == normalizedCategory && t.Path == trimmedPath);
            if (isDuplicate)
            {
                throw new InvalidOperationException("That path has already been added.");
            }

            var target = new EncryptionTarget(Guid.NewGuid(), normalizedCategory, trimmedPath);
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
