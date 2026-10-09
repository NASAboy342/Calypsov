namespace Calypsov.Repositories;

/// <summary>A file's name plus its last-write time (UTC), as returned by <see cref="ISettingRepository.ListFiles"/>.</summary>
public sealed record FileEntry(string Name, DateTime LastModifiedUtc);
