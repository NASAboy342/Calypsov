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

    /// <summary>Turbo Zip only kicks in once a category has more items than this — below it,
    /// splitting the work across threads isn't worth the overhead.</summary>
    private const int TurboZipFolderThreshold = 20;

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

    public StorageEncryptionSettingsService(ISettingRepository settingRepository, IBrowserProfileService browserProfileService)
    {
        _settingRepository = settingRepository;
        _browserProfileService = browserProfileService;
    }

    public EncryptionTarget AddTarget(EnumTargetCategory category, string path)
    {
        var newTargetToAdd = new EncryptionTarget(Guid.NewGuid(), category, path);
        lock (_lock)
        {
            var appSetting = GetAppSetting();
            CheckIfTargetAlreadyAddBefore(category, path, appSetting);
            CheckIfTargetIsASubTargetFromExistingTarget(category, path, appSetting);
            appSetting.EncryptionTargets.Add(newTargetToAdd);
            SaveAppSetting();
        }
        return newTargetToAdd;
    }

    private void CheckIfTargetIsASubTargetFromExistingTarget(EnumTargetCategory category, string path, AppSetting appSetting)
    {
        if(category != EnumTargetCategory.Folder)
            return;
        var existingTargetFolders = appSetting.EncryptionTargets.Where(t => t.Category == EnumTargetCategory.Folder).Select(t => t.Path).ToList();
        AddBrowserProfileFolderToFolderToEncryp(existingTargetFolders);
        foreach (var folder in existingTargetFolders)
        {
            if (IsSubFolder(folder, path))
                throw new Exception($"This folder under an existing configed folder: {folder}. So no need to add it.");
        }
    }

    private void CheckIfTargetAlreadyAddBefore(EnumTargetCategory category, string path, AppSetting appSetting)
    {
        if (appSetting.EncryptionTargets.Any(t => t.Category == category && t.Path.Equals(path)))
        {
            throw new Exception("EncryptionTarget already set. Will not add duplicated one");
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
        var appSetting = GetAppSetting();
        return appSetting.EncryptionTargets;
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
    }

    public bool IsEncryptionEnabled()
    {
        lock (_lock)
        {
            var appSetting = GetAppSetting();
            return appSetting.IsEncrypted;
        }
    }

    public bool RemoveTarget(Guid id)
    {
        lock (_lock)
        {
            var appSetting = GetAppSetting();
            appSetting.EncryptionTargets.RemoveAll(t => t.Id.Equals(id));
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
            var appSetting = GetAppSetting();
            appSetting.IsEncrypted = enabled;
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
        lock (_lock)
        {
            var appSetting = GetAppSetting();
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
        lock (_lock)
        {
            var appSetting = GetAppSetting();
            appSetting.IsRecordLog = enabled;
            SaveAppSetting();
            return appSetting.IsRecordLog;
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
            _progress = new EncryptionProgress(isRunning, completed, total, currentItem, threads);
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
            var appSetting = GetAppSetting();
            if (!appSetting.IsEncrypted)
            {
                ProcessEncryption();
            }
            else
            {
                ProcessDecryption();
            }
            appSetting.IsEncrypted = !appSetting.IsEncrypted;
            SaveAppSetting();
        }
        return GetAppSetting().IsEncrypted;
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
        var filesToEncryp = GetAppSetting().EncryptionTargets.Where(t => t.Category == EnumTargetCategory.File).Select(t => t.Path).ToList();
        var foldersToEncryp = GetAppSetting().EncryptionTargets.Where(t => t.Category == EnumTargetCategory.Folder).Select(t => t.Path).ToList();
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

        try
        {
            EncrypFiles(filesToEncryp, OnFileProcessed);
            EncrypFolders(foldersToEncryp, OnFileProcessed);
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
        DelectAllTargetFolderFromOriginalLocations(foldersToEncryp);
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
        var profileFolderPaths = _browserProfileService.GetSelectedProfile().Values
            .Select(p => p.folderPath)
            .OfType<string>();

        foldersToEncryp.AddRange(profileFolderPaths);
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
        DelectAllTargetFileFromOriginalLocations(filesToEncryp);
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
}
