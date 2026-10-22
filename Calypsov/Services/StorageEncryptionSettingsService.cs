using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Calypsov.Helpers;
using Calypsov.Models;
using Calypsov.Repositories;

namespace Calypsov.Services;

public class StorageEncryptionSettingsService : IEncryptionSettingsService
{
    private const string SettingsFileName = "settings.json";

    /// <summary>Pre-profiles browser-profile-selection file, read exactly once during
    /// <see cref="MigrateToProfilesIfNeeded"/> and never written to again afterward — selections now
    /// live on each <see cref="ProtectionProfile"/> instead.</summary>
    private const string LegacyBrowserProfilesFileName = "browserProfiles.json";

    private const string DefaultProfileName = "Default";
    private const string DefaultProfileColor = "teal";
    private const string DefaultProfileIcon = "shield";

    /// <summary>Turbo Zip only kicks in once a category has more items than this — below it,
    /// splitting the work across threads isn't worth the overhead.</summary>
    private const int TurboZipFolderThreshold = 20;

    /// <summary>Shown (and used as the thrown <see cref="InvalidOperationException"/>'s message)
    /// whenever an AppSetting-backed setting is changed while files are protected.</summary>
    private const string SettingsLockedMessage = "Setting cannot be change while files are protected. Turn off the protection first before change any setting.";

    private readonly ISettingRepository _settingRepository;
    private AppSetting? _appSetting;
    private readonly object _lock = new();
    private readonly object _lockEncryption = new();
    private readonly string _encryptedFoldersFileName = "CalypsovFolders.zip";
    private readonly string _encryptedFilesFileName = "CalypsovFiles.zip";
    private readonly IBrowserProfileService _browserProfileService;

    // Guards _progress only — deliberately separate from _lock, which is held for the entire
    // duration of ToggleEncryption(). If GetProgress() shared that lock, every progress poll
    // would block until the whole encrypt/decrypt run finished, defeating the point of polling.
    private readonly object _lockProgress = new();
    private EncryptionProgress _progress = new(IsRunning: false, Completed: 0, Total: 0, CurrentItem: null, Threads: Array.Empty<ZipThreadProgress>());

    // Per-Turbo-Zip-worker snapshots, keyed by thread/part index. ConcurrentDictionary since
    // multiple Parallel.For workers report into it at once; SetProgress reads a snapshot of it
    // under _lockProgress when building the overall EncryptionProgress.
    private readonly ConcurrentDictionary<int, ZipThreadProgress> _threadProgress = new();

    // Cancellation of an in-flight encrypt run. Non-null only while ProcessEncryption() is in its
    // zipping phase; guarded by _lockProgress (same reasoning as _progress — CancelEncryption()
    // must never wait on _lock, which ToggleEncryption() holds for the whole run).
    private CancellationTokenSource? _cancelSource;

    public StorageEncryptionSettingsService(ISettingRepository settingRepository, IBrowserProfileService browserProfileService)
    {
        _settingRepository = settingRepository;
        _browserProfileService = browserProfileService;
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if an encrypt/decrypt run is currently in
    /// progress. Deliberately checked *before* acquiring <see cref="_lock"/> in each setting-mutating
    /// method below, rather than inside it — <see cref="ToggleEncryption"/> holds that exact lock for
    /// its *entire* run, so checking only after acquiring it would make these calls silently block
    /// until the run finishes (then succeed or fail based on the post-run state) instead of failing
    /// fast with a clear "settings are locked" error.
    /// </summary>
    private void EnsureNotCurrentlyToggling()
    {
        if (GetProgress().IsRunning)
        {
            throw new InvalidOperationException(SettingsLockedMessage);
        }
    }

    /// <summary>Throws <see cref="InvalidOperationException"/> if the profile is already encrypted.
    /// Call from inside a setting-mutating method, under <see cref="_lock"/>, alongside
    /// <see cref="EnsureNotCurrentlyToggling"/> — together they cover "protected" meaning either
    /// already encrypted, or an encrypt/decrypt run still in progress. Only the current profile can
    /// ever be encrypted (switching/creating/deleting profiles is itself blocked while one is), so
    /// this is always called with <see cref="GetCurrentProfile"/>'s result.</summary>
    private static void EnsureNotEncrypted(ProtectionProfile profile)
    {
        if (profile.IsEncrypted)
        {
            throw new InvalidOperationException(SettingsLockedMessage);
        }
    }

    public EncryptionTarget AddTarget(EnumTargetCategory category, string path)
    {
        EnsureNotCurrentlyToggling();
        var newTargetToAdd = new EncryptionTarget(Guid.NewGuid(), category, path);
        lock (_lock)
        {
            var currentProfile = GetCurrentProfile();
            EnsureNotEncrypted(currentProfile);
            CheckIfTargetAlreadyAddBefore(category, path, currentProfile);
            CheckIfTargetIsASubTargetFromExistingTarget(category, path, currentProfile);
            currentProfile.EncryptionTargets.Add(newTargetToAdd);
            SaveAppSetting();
        }
        return newTargetToAdd;
    }

    private void CheckIfTargetIsASubTargetFromExistingTarget(EnumTargetCategory category, string path, ProtectionProfile profile)
    {
        if(category != EnumTargetCategory.Folder)
            return;
        var existingTargetFolders = profile.EncryptionTargets.Where(t => t.Category == EnumTargetCategory.Folder).Select(t => t.Path).ToList();
        AddBrowserProfileFolderToFolderToEncryp(existingTargetFolders);
        foreach (var folder in existingTargetFolders)
        {
            if (IsSubFolder(folder, path))
                // InvalidOperationException, not a plain Exception: the path itself is fine, it just
                // conflicts with an already-configured target - matches IEncryptionSettingsService's
                // documented contract ("InvalidOperationException if the path is already configured"),
                // and EndpointHelpers.Try maps it to 409 Conflict instead of a misleading 500.
                throw new InvalidOperationException($"This folder under an existing configed folder: {folder}. So no need to add it.");
        }
    }

    private void CheckIfTargetAlreadyAddBefore(EnumTargetCategory category, string path, ProtectionProfile profile)
    {
        if (profile.EncryptionTargets.Any(t => t.Category == category && t.Path.Equals(path)))
        {
            // Same reasoning as the sub-folder check above: the path is already configured, which is
            // exactly the documented "InvalidOperationException if the path is already configured"
            // case on IEncryptionSettingsService.AddTarget - maps to 409 Conflict, not 500.
            throw new InvalidOperationException("EncryptionTarget already set. Will not add duplicated one");
        }
    }

    private bool IsSubFolder(string parentFolder,string childFolder)
    {
        var fullParentPath = Path.GetFullPath(parentFolder)
            .TrimEnd(Path.DirectorySeparatorChar);

        var fullChildPath = Path.GetFullPath(childFolder)
            .TrimEnd(Path.DirectorySeparatorChar);

        var relativePath = Path.GetRelativePath(
            fullParentPath,
            fullChildPath);

        // "." means they are the same folder
        // ".." means the child is outside the parent
        // "../..." also means outside the parent
        return relativePath != "."
            && !relativePath.Equals("..")
            && !relativePath.StartsWith(
                ".." + Path.DirectorySeparatorChar);
    }

    public IReadOnlyList<EncryptionTarget> GetTargets()
    {
        return GetCurrentProfile().EncryptionTargets;
    }

    private AppSetting GetAppSetting()
    {
        if(_appSetting == null)
        {
            LoadAppSetting();
        }
        return _appSetting!;
    }

    private void LoadAppSetting()
    {
        _appSetting = _settingRepository.Load<AppSetting>(SettingsFileName) ?? new AppSetting();
        MigrateToProfilesIfNeeded();
    }

    /// <summary>
    /// One-time migration for a settings.json written before protection profiles existed (or a
    /// brand-new install, which hits this same path with an empty AppSetting): folds the old flat
    /// EncryptionTargets list, the old top-level IsEncrypted flag, and whatever's in the legacy,
    /// separately-stored browserProfiles.json into a single "Default" profile the user can rename/
    /// recolor/re-icon like any other - per the decision that existing config becomes the default
    /// profile. Runs at most once per AppSetting instance: Profiles is non-empty afterward, forever,
    /// so this is a no-op on every later load.
    /// </summary>
    private void MigrateToProfilesIfNeeded()
    {
        if (_appSetting!.Profiles.Count > 0)
            return;

        var legacyBrowserSelections = _settingRepository.Load<Dictionary<BrowserType, string?>>(LegacyBrowserProfilesFileName)
            ?? new Dictionary<BrowserType, string?>();

        var defaultProfile = new ProtectionProfile
        {
            Id = Guid.NewGuid(),
            Name = DefaultProfileName,
            Color = DefaultProfileColor,
            Icon = DefaultProfileIcon,
            IsEncrypted = _appSetting.IsEncrypted,
            EncryptionTargets = _appSetting.EncryptionTargets ?? new List<EncryptionTarget>(),
            BrowserProfileSelections = legacyBrowserSelections,
        };

        _appSetting.Profiles.Add(defaultProfile);
        _appSetting.CurrentProfileId = defaultProfile.Id;
        _appSetting.EncryptionTargets = null;
        SaveAppSetting();
    }

    /// <summary>The profile whose targets/browser-profile-selections/encryption state every other
    /// method operates on. Always succeeds after <see cref="LoadAppSetting"/>'s migration has run at
    /// least once (which it has by the time this is ever called), since Profiles is never empty and
    /// CurrentProfileId always points at one of its entries.</summary>
    private ProtectionProfile GetCurrentProfile()
    {
        var appSetting = GetAppSetting();
        return appSetting.Profiles.First(p => p.Id == appSetting.CurrentProfileId);
    }

    public bool IsEncryptionEnabled()
    {
        lock (_lock)
        {
            return GetCurrentProfile().IsEncrypted;
        }
    }

    public bool RemoveTarget(Guid id)
    {
        EnsureNotCurrentlyToggling();
        lock (_lock)
        {
            var currentProfile = GetCurrentProfile();
            EnsureNotEncrypted(currentProfile);
            currentProfile.EncryptionTargets.RemoveAll(t => t.Id.Equals(id));
            SaveAppSetting();
        }
        return true;
    }

    private void SaveAppSetting()
    {
        _settingRepository.Save(SettingsFileName, _appSetting!);
    }

    public bool SetEncryptionEnabled(bool enabled)
    {
        lock (_lock)
        {
            GetCurrentProfile().IsEncrypted = enabled;
            SaveAppSetting();
        }
        return true;
    }

    public ZipSettings GetZipSettings()
    {
        lock (_lock)
        {
            var appSetting = GetAppSetting();
            return new ZipSettings(appSetting.IsUseTurboZip, appSetting.IsEncryptZippedFile);
        }
    }

    public ZipSettings SetZipSettings(bool isUseTurboZip, bool isEncryptZippedFile)
    {
        EnsureNotCurrentlyToggling();
        lock (_lock)
        {
            var appSetting = GetAppSetting();
            EnsureNotEncrypted(GetCurrentProfile());
            appSetting.IsUseTurboZip = isUseTurboZip;
            appSetting.IsEncryptZippedFile = isEncryptZippedFile;
            SaveAppSetting();
            return new ZipSettings(appSetting.IsUseTurboZip, appSetting.IsEncryptZippedFile);
        }
    }

    public bool GetIsRecordLogEnabled()
    {
        lock (_lock)
        {
            var appSetting = GetAppSetting();
            return appSetting.IsRecordLog;
        }
    }

    public bool SetIsRecordLogEnabled(bool enabled)
    {
        EnsureNotCurrentlyToggling();
        lock (_lock)
        {
            var appSetting = GetAppSetting();
            EnsureNotEncrypted(GetCurrentProfile());
            appSetting.IsRecordLog = enabled;
            SaveAppSetting();
            return appSetting.IsRecordLog;
        }
    }

    public bool GetIsEncryptAppSettingEnabled()
    {
        lock (_lock)
        {
            var appSetting = GetAppSetting();
            return appSetting.IsEncryptAppSetting;
        }
    }

    public bool SetIsEncryptAppSettingEnabled(bool enabled)
    {
        EnsureNotCurrentlyToggling();
        lock (_lock)
        {
            var appSetting = GetAppSetting();
            EnsureNotEncrypted(GetCurrentProfile());
            appSetting.IsEncryptAppSetting = enabled;
            SaveAppSetting();
            return appSetting.IsEncryptAppSetting;
        }
    }

    public EncryptionProgress GetProgress()
    {
        lock (_lockProgress)
        {
            return _progress;
        }
    }

    private void SetProgress(bool isRunning, int completed, int total, string? currentItem)
    {
        lock (_lockProgress)
        {
            IReadOnlyList<ZipThreadProgress> threads = _threadProgress.IsEmpty
                ? Array.Empty<ZipThreadProgress>()
                : _threadProgress.Values.OrderBy(t => t.ThreadIndex).ToList();
            _progress = new EncryptionProgress(isRunning, completed, total, currentItem, threads, CanCancel: _cancelSource is { IsCancellationRequested: false });
        }
    }

    public void CancelEncryption()
    {
        lock (_lockProgress)
        {
            if (_cancelSource == null || _cancelSource.IsCancellationRequested)
            {
                throw new InvalidOperationException("There is nothing to cancel right now.");
            }
            _cancelSource.Cancel();
            _progress = _progress with { CanCancel = false };
        }
    }

    /// <summary>Opens (or, with null, closes) the window in which <see cref="CancelEncryption"/> is allowed.</summary>
    private void SetCancelSource(CancellationTokenSource? source)
    {
        lock (_lockProgress)
        {
            _cancelSource = source;
            _progress = _progress with { CanCancel = source != null };
        }
    }

    /// <summary>Records one Turbo Zip worker's own progress through its chunk — picked up by the
    /// next <see cref="SetProgress"/> call's <see cref="EncryptionProgress.Threads"/> snapshot.</summary>
    private void ReportThreadProgress(int threadIndex, int completed, int total, string currentItem)
    {
        _threadProgress[threadIndex] = new ZipThreadProgress(threadIndex, completed, total, currentItem);
    }

    private void ClearThreadProgress() => _threadProgress.Clear();

    public bool ToggleEncryption()
    {
        lock (_lock)
        {
            var currentProfile = GetCurrentProfile();
            if (!currentProfile.IsEncrypted)
            {
                try
                {
                    ProcessEncryption();
                }
                catch (OperationCanceledException)
                {
                    // User cancelled while zipping — everything was already rolled back, so the
                    // profile simply stays unprotected.
                    return false;
                }
            }
            else
            {
                ProcessDecryption();
            }
            currentProfile.IsEncrypted = !currentProfile.IsEncrypted;
            SaveAppSetting();
            return currentProfile.IsEncrypted;
        }
    }

    private void ProcessDecryption()
    {
        var encrypFolderPath = GetEncrypFolderPath();
        var totalFiles = Zip.CountManifestEntries(Path.Combine(encrypFolderPath, _encryptedFilesFileName))
            + GetEncryptedFolderZipPaths().Sum(Zip.CountManifestEntries);

        var completed = 0;
        ClearThreadProgress();
        SetProgress(isRunning: true, completed: 0, total: totalFiles, currentItem: null);

        void OnFileProcessed(string currentItem)
        {
            completed++;
            SetProgress(isRunning: true, completed: completed, total: totalFiles, currentItem: currentItem);
        }

        try
        {
            DecrypFiles(OnFileProcessed);
            DecrypFolders(OnFileProcessed);
        }
        finally
        {
            SetProgress(isRunning: false, completed: totalFiles, total: totalFiles, currentItem: null);
            ClearThreadProgress();
        }
    }

    /// <summary>
    /// A Turbo Zip run splits folders across <c>CalypsovFolders.part1.zip</c>, <c>.part2.zip</c>, etc.
    /// instead of one <c>CalypsovFolders.zip</c> — this finds whichever form is actually on disk so
    /// decrypt (and cleanup) work the same either way. The two forms never coexist for the same run.
    /// </summary>
    private IEnumerable<string> GetEncryptedFolderZipPaths()
    {
        var encrypFolderPath = GetEncrypFolderPath();
        var singleFilePath = Path.Combine(encrypFolderPath, _encryptedFoldersFileName);
        if (File.Exists(singleFilePath))
        {
            return new[] { singleFilePath };
        }

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(_encryptedFoldersFileName);
        var extension = Path.GetExtension(_encryptedFoldersFileName);
        var searchPattern = $"{nameWithoutExtension}.part*{extension}";

        return Directory.EnumerateFiles(encrypFolderPath, searchPattern)
            .OrderBy(GetTurboZipPartIndex);
    }

    private void DecrypFolders(Action<string>? onFileProcessed = null)
    {
        lock (_lockEncryption)
        {
            var zipPaths = GetEncryptedFolderZipPaths().ToList();
            if (zipPaths.Count == 0)
                return;

            foreach (var zipPath in zipPaths)
            {
                Zip.UnzipFile(zipPath, onFileProcessed);
            }
            CleanupEncryptedFolderFile();
        }
    }

    private void DecrypFiles(Action<string>? onFileProcessed = null)
    {
        lock (_lockEncryption)
        {
            var encrypFolderPath = GetEncrypFolderPath();
            var encrypFileName = _encryptedFilesFileName;
            var encrypFilePath = Path.Combine(encrypFolderPath, encrypFileName);
            if(!File.Exists(encrypFilePath))
                return;
            Zip.UnzipFile(encrypFilePath, onFileProcessed);
            CleanupEncryptedFilesFile();
        }
    }

    private void ProcessEncryption()
    {
        var currentProfileTargets = GetCurrentProfile().EncryptionTargets;
        var filesToEncryp = currentProfileTargets.Where(t => t.Category == EnumTargetCategory.File).Select(t => t.Path).ToList();
        var foldersToEncryp = currentProfileTargets.Where(t => t.Category == EnumTargetCategory.Folder).Select(t => t.Path).ToList();
        AddBrowserProfileFolderToFolderToEncryp(foldersToEncryp);

        var totalFiles = filesToEncryp.Count(File.Exists)
            + foldersToEncryp.Where(Directory.Exists).Sum(f => Directory.GetFiles(f, "*", SearchOption.AllDirectories).Length);

        var completed = 0;
        ClearThreadProgress();
        SetProgress(isRunning: true, completed: 0, total: totalFiles, currentItem: null);

        // Interlocked since, once Turbo Zip is running, several worker threads can report a
        // file completing at the same moment — a plain "completed++" would lose increments.
        void OnFileProcessed(string currentItem)
        {
            var newCompleted = Interlocked.Increment(ref completed);
            SetProgress(isRunning: true, completed: newCompleted, total: totalFiles, currentItem: currentItem);
        }

        using var cancelSource = new CancellationTokenSource();
        var cancelToken = cancelSource.Token;

        // Cancelling is only possible while zipping: both archives are written first, then (and only
        // then) the originals are deleted, so a cancel never has to restore anything.
        void OnFileProcessedUnlessCancelled(string currentItem)
        {
            cancelToken.ThrowIfCancellationRequested();
            OnFileProcessed(currentItem);
        }

        try
        {
            SetCancelSource(cancelSource);
            try
            {
                EncrypFiles(filesToEncryp, OnFileProcessedUnlessCancelled);
                EncrypFolders(foldersToEncryp, OnFileProcessedUnlessCancelled);
                cancelToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                CleanupEncryptedFilesFile();
                CleanupEncryptedFolderFile();
                throw;
            }
            finally
            {
                // Closing the window before deleting starts means a late cancel request is rejected
                // instead of racing the deletion.
                SetCancelSource(null);
            }

            try
            {
                DelectAllTargetFileFromOriginalLocations(filesToEncryp);
            }
            catch
            {
                // The files were restored by the delete step itself; the folders archive written
                // before it would otherwise be left behind and block the next run.
                CleanupEncryptedFolderFile();
                throw;
            }
            DelectAllTargetFolderFromOriginalLocations(foldersToEncryp);
        }
        finally
        {
            SetProgress(isRunning: false, completed: totalFiles, total: totalFiles, currentItem: null);
            ClearThreadProgress();
        }
    }

    private void EncrypFolders(List<string> foldersToEncryp, Action<string>? onFileProcessed = null)
    {
        lock (_lockEncryption)
        {
            if (!foldersToEncryp.Any()) return;
            CheckIfCanAccessPaths(foldersToEncryp);
            try
            {
                var encrypFolderPath = GetEncrypFolderPath();
                if (ShouldUseTurboZip(foldersToEncryp.Count))
                {
                    ZipFoldersInParallel(foldersToEncryp, encrypFolderPath, onFileProcessed);
                }
                else
                {
                    Zip.ZipFolders(foldersToEncryp, encrypFolderPath, _encryptedFoldersFileName, onFileProcessed);
                }
            }
            catch (UnauthorizedAccessException e)
            {
                CleanupEncryptedFolderFile();
                throw new Exception($"{e.Message} Please go to System Settings → Privacy & Security → Full Disk Access and allow for Calypsov");
            }
            catch (Exception e)
            {
                CleanupEncryptedFolderFile();
                throw;
            }
        }
    }

    private bool ShouldUseTurboZip(int folderCount) =>
        GetAppSetting().IsUseTurboZip && folderCount > TurboZipFolderThreshold;

    /// <summary>
    /// Turbo Zip: splits <paramref name="folders"/> evenly across up to <see cref="Environment.ProcessorCount"/>
    /// workers, each zipping its own chunk into its own numbered part file (<c>CalypsovFolders.part1.zip</c>,
    /// <c>.part2.zip</c>, …) via <see cref="Zip.ZipFolders"/> — same helper as the single-archive path, just
    /// called once per chunk, so each worker writes to its own <see cref="System.IO.Compression.ZipArchive"/>
    /// instance and there's no shared-archive thread-safety to worry about.
    /// </summary>
    private void ZipFoldersInParallel(List<string> folders, string encrypFolderPath, Action<string>? onFileProcessed)
    {
        var partCount = Math.Min(Environment.ProcessorCount, folders.Count);
        var chunks = ChunkEvenly(folders, partCount);

        try
        {
            Parallel.For(0, chunks.Count, new ParallelOptions { MaxDegreeOfParallelism = partCount }, threadIndex =>
            {
                var chunk = chunks[threadIndex];
                var partFileName = GetTurboZipPartFileName(_encryptedFoldersFileName, threadIndex);
                var partTotal = chunk.Where(Directory.Exists).Sum(f => Directory.GetFiles(f, "*", SearchOption.AllDirectories).Length);
                var partCompleted = 0;

                void OnPartFileProcessed(string currentItem)
                {
                    partCompleted++;
                    ReportThreadProgress(threadIndex, partCompleted, partTotal, currentItem);
                    onFileProcessed?.Invoke(currentItem);
                }

                Zip.ZipFolders(chunk, encrypFolderPath, partFileName, OnPartFileProcessed);
            });
        }
        catch (AggregateException ex) when (ex.InnerException != null)
        {
            // Unwrap so callers see the same exception types (UnauthorizedAccessException, etc.)
            // they'd get from the single-threaded path, instead of always an AggregateException.
            throw ex.InnerException;
        }
    }

    /// <summary>Round-robins items into <paramref name="chunkCount"/> lists as evenly as possible.
    /// Every chunk gets at least one item as long as <paramref name="chunkCount"/> &lt;= items.Count.</summary>
    private static List<List<string>> ChunkEvenly(List<string> items, int chunkCount)
    {
        var chunks = new List<List<string>>(chunkCount);
        for (var i = 0; i < chunkCount; i++)
        {
            chunks.Add(new List<string>());
        }

        for (var i = 0; i < items.Count; i++)
        {
            chunks[i % chunkCount].Add(items[i]);
        }

        return chunks;
    }

    /// <summary>"CalypsovFolders.zip" + part 0 (0-based) -> "CalypsovFolders.part1.zip" (1-based, for humans).</summary>
    private static string GetTurboZipPartFileName(string baseFileName, int partIndex)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(baseFileName);
        var extension = Path.GetExtension(baseFileName);
        return $"{nameWithoutExtension}.part{partIndex + 1}{extension}";
    }

    /// <summary>Extracts the numeric suffix from a "*.partN.*" file name, for sorting part files
    /// in human order (part2 before part10) rather than plain string order.</summary>
    private static int GetTurboZipPartIndex(string path)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        var partSegment = Path.GetExtension(nameWithoutExtension).TrimStart('.'); // "...part3" -> "part3"
        return int.TryParse(partSegment.Replace("part", ""), out var index) ? index : 0;
    }

    private void AddBrowserProfileFolderToFolderToEncryp(List<string> foldersToEncryp)
    {
        foreach (var (browser, profileId) in GetCurrentProfile().BrowserProfileSelections)
        {
            if (profileId == null)
                continue;

            var matchedProfile = _browserProfileService.GetProfiles(browser).FirstOrDefault(p => p.Id == profileId);
            if (matchedProfile?.folderPath != null)
            {
                foldersToEncryp.Add(matchedProfile.folderPath);
            }
        }
    }

    public bool CheckIfCanReadFiles(List<string> filePaths)
    {
        try
        {
            foreach(var filePath in filePaths)
            {
                using var stream = File.Open(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);
            }
            return true;
        }
        catch (UnauthorizedAccessException e)
        {
            CleanupEncryptedFilesFile();
            throw new Exception($"{e.Message} Please go to System Settings → Privacy & Security → Full Disk Access and allow for Calypsov");
        }
        catch (Exception e)
        {
            CleanupEncryptedFilesFile();
            throw;
        }
    }
    public bool CheckIfCanAccessPaths(List<string> paths)
    {
        try
        {
            foreach(var path in paths)
            {
                // Actually try to enumerate the directory
                using var enumerator = Directory.EnumerateFileSystemEntries(path).GetEnumerator();

                // Move once to force the OS to check access
                _ = enumerator.MoveNext();
            }

            return true;
        }
        catch (UnauthorizedAccessException e)
        {
            CleanupEncryptedFolderFile();
            throw new Exception($"{e.Message} Please go to System Settings → Privacy & Security → Full Disk Access and allow for Calypsov");
        }
        catch (Exception e)
        {
            CleanupEncryptedFolderFile();
            throw;
        }
    }

    private void DelectAllTargetFolderFromOriginalLocations(List<string> foldersToEncryp)
    {
        var folderPathBeingDeleted = "";
        try
        {
            foreach (var folderPath in foldersToEncryp)
            {
                folderPathBeingDeleted = folderPath;
                Directory.Delete(folderPath, true);
            }
        }
        catch (Exception e)
        {
            DecrypFolders();
            throw new Exception($"{e.Message} : Fail to delete folder \"{folderPathBeingDeleted}\". Encryption has been canceled. No thing is encrypted.");
        }

    }

    private void CleanupEncryptedFolderFile()
    {
        foreach (var zipPath in GetEncryptedFolderZipPaths())
        {
            File.Delete(zipPath);
        }
    }

    private string GetEncrypFolderPath()
    {
        var settingPath = _settingRepository.GetAppDataFolder();
        var encrypFolderName = "Encryption";
        var encrypFolderPath = Path.Combine(settingPath, encrypFolderName);
        Directory.CreateDirectory(encrypFolderPath);
        return encrypFolderPath;
    }

    private void EncrypFiles(List<string> filesToEncryp, Action<string>? onFileProcessed = null)
    {
        lock (_lockEncryption)
        {
            if (!filesToEncryp.Any()) return;
            CheckIfCanReadFiles(filesToEncryp);
            try
            {
                var encrypFolderPath = GetEncrypFolderPath();
                var encrypFileName = _encryptedFilesFileName;
                Zip.ZipFiles(filesToEncryp, encrypFolderPath, encrypFileName, onFileProcessed);
            }
            catch (UnauthorizedAccessException e)
            {
                CleanupEncryptedFilesFile();
                throw new Exception($"{e.Message} Please go to System Settings → Privacy & Security → Full Disk Access and allow for Calypsov");
            }
            catch (Exception e)
            {
                CleanupEncryptedFilesFile();
                throw;
            }
        }
    }

    private void DelectAllTargetFileFromOriginalLocations(List<string> filesToEncryp)
    {
        var filePathBeingDeleted = "";
        try
        {
            foreach (var filePath in filesToEncryp)
            {
                filePathBeingDeleted = filePath;
                File.Delete(filePath);
            }
        }
        catch (Exception e)
        {
            DecrypFiles();
            throw new Exception($"{e.Message} : Fail to delete file \"{filePathBeingDeleted}\". Encryption has been canceled. No thing is encrypted.");
        }
    }

    private void CleanupEncryptedFilesFile()
    {
        var encrypFolderPath = GetEncrypFolderPath();
        var encrypFileName = _encryptedFilesFileName;
        var encrypFilePath = Path.Combine(encrypFolderPath, encrypFileName);
        if (File.Exists(encrypFilePath))
        {
            File.Delete(encrypFilePath);
        }
    }

    public string? GetSelectedBrowserProfileId(BrowserType browser)
    {
        lock (_lock)
        {
            var selections = GetCurrentProfile().BrowserProfileSelections;
            return selections.TryGetValue(browser, out var profileId) ? profileId : null;
        }
    }

    public string? SetSelectedBrowserProfileId(BrowserType browser, string? profileId)
    {
        EnsureNotCurrentlyToggling();
        lock (_lock)
        {
            var currentProfile = GetCurrentProfile();
            EnsureNotEncrypted(currentProfile);
            currentProfile.BrowserProfileSelections[browser] = profileId;
            SaveAppSetting();
            return profileId;
        }
    }

    // --- Protection profiles ---

    public IReadOnlyList<ProtectionProfile> GetProfiles()
    {
        lock (_lock)
        {
            return GetAppSetting().Profiles.ToList();
        }
    }

    public Guid GetCurrentProfileId()
    {
        lock (_lock)
        {
            return GetAppSetting().CurrentProfileId;
        }
    }

    public ProtectionProfile CreateProfile(string name, string color, string icon)
    {
        ValidateProfileFields(name, color, icon);
        EnsureNotCurrentlyToggling();
        lock (_lock)
        {
            var appSetting = GetAppSetting();
            EnsureNotEncrypted(GetCurrentProfile());

            var newProfile = new ProtectionProfile
            {
                Id = Guid.NewGuid(),
                Name = name,
                Color = color,
                Icon = icon,
            };
            appSetting.Profiles.Add(newProfile);
            appSetting.CurrentProfileId = newProfile.Id;
            SaveAppSetting();
            return newProfile;
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
            SaveAppSetting();
            return profile;
        }
    }

    public ProtectionProfile SwitchProfile(Guid profileId)
    {
        EnsureNotCurrentlyToggling();
        lock (_lock)
        {
            EnsureNotEncrypted(GetCurrentProfile());
            var target = FindProfileOrThrow(profileId);
            GetAppSetting().CurrentProfileId = target.Id;
            SaveAppSetting();
            return target;
        }
    }

    public void DeleteProfile(Guid profileId)
    {
        EnsureNotCurrentlyToggling();
        lock (_lock)
        {
            EnsureNotEncrypted(GetCurrentProfile());
            var appSetting = GetAppSetting();
            var target = FindProfileOrThrow(profileId);
            if (appSetting.Profiles.Count <= 1)
            {
                throw new ArgumentException("Cannot delete the only remaining profile.");
            }

            var wasCurrent = appSetting.CurrentProfileId == target.Id;
            appSetting.Profiles.Remove(target);
            if (wasCurrent)
            {
                // The deleted profile was active - fall back to the first remaining one so there's
                // always a valid "current" profile, since every target/browser-selection/encryption
                // operation assumes one exists.
                appSetting.CurrentProfileId = appSetting.Profiles[0].Id;
            }
            SaveAppSetting();
        }
    }

    private ProtectionProfile FindProfileOrThrow(Guid profileId)
    {
        var profile = GetAppSetting().Profiles.FirstOrDefault(p => p.Id == profileId);
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
