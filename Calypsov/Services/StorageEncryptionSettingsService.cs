using System;
using System.Diagnostics;
using Calypsov.Helpers;
using Calypsov.Models;
using Calypsov.Repositories;

namespace Calypsov.Services;

public class StorageEncryptionSettingsService : IEncryptionSettingsService
{
    private const string SettingsFileName = "settings.json";

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
    private EncryptionProgress _progress = new(IsRunning: false, Completed: 0, Total: 0, CurrentItem: null);

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
            _progress = new EncryptionProgress(isRunning, completed, total, currentItem);
        }
    }

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
            + Zip.CountManifestEntries(Path.Combine(encrypFolderPath, _encryptedFoldersFileName));

        var completed = 0;
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
        }
    }

    private void DecrypFolders(Action<string>? onFileProcessed = null)
    {
        lock (_lockEncryption)
        {
            var encrypFolderPath = GetEncrypFolderPath();
            var encrypFileName = _encryptedFoldersFileName;
            var encrypFilePath = Path.Combine(encrypFolderPath, encrypFileName);
            if(!File.Exists(encrypFilePath))
                return;
            Zip.UnzipFile(encrypFilePath, onFileProcessed);
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
        SetProgress(isRunning: true, completed: 0, total: totalFiles, currentItem: null);

        void OnFileProcessed(string currentItem)
        {
            completed++;
            SetProgress(isRunning: true, completed: completed, total: totalFiles, currentItem: currentItem);
        }

        try
        {
            EncrypFiles(filesToEncryp, OnFileProcessed);
            EncrypFolders(foldersToEncryp, OnFileProcessed);
        }
        finally
        {
            SetProgress(isRunning: false, completed: totalFiles, total: totalFiles, currentItem: null);
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
                var encrypFileName = _encryptedFoldersFileName;
                Zip.ZipFolders(foldersToEncryp, encrypFolderPath, encrypFileName, onFileProcessed);
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
        var encrypFolderPath = GetEncrypFolderPath();
        var encrypFileName = _encryptedFoldersFileName;
        var encrypFolderFilePath = Path.Combine(encrypFolderPath,encrypFileName);
        if (File.Exists(encrypFolderFilePath))
        {
            File.Delete(encrypFolderFilePath);
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
