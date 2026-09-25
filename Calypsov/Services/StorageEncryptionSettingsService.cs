using System;
using System.Diagnostics;
using Calypsov.Helpers;
using Calypsov.Models;
using Newtonsoft.Json;

namespace Calypsov.Services;

public class StorageEncryptionSettingsService : IEncryptionSettingsService
{
    private AppSetting? _appSetting;
    private readonly object _lock = new();
    private readonly object _lockFile = new();
    private readonly object _lockEncryption = new();
    private readonly string _encryptedFoldersFileName = "CalypsovFolders.zip";
    private readonly string _encryptedFilesFileName = "CalypsovFiles.zip";

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
        lock (_lockFile)
        {
            try
            {
                var settingPath = GetSettingFilePath();
                var json = File.ReadAllText(settingPath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    _appSetting = new AppSetting();
                    return;
                }

                _appSetting = JsonConvert.DeserializeObject<AppSetting>(json);
            }
            catch
            {
                _appSetting = new AppSetting();
            }
            
        }
    }

    private string GetSettingFilePath()
    {
        var appSettingFileName = "settings.json";
        var appDirectory = GetSettingPath();
        var settingsPath = Path.Combine(appDirectory, appSettingFileName);
        IfFileNotExistCreateOne(settingsPath);
        return settingsPath;
    }

    private void IfFileNotExistCreateOne(string filePath)
    {
        if (!File.Exists(filePath))
        {
            using var stream = File.Create(filePath);
        }
    }

    private string GetSettingPath()
    {
        var appFolderName = "Calypsov";
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDirectory = Path.Combine(appData, appFolderName);
        IfFolderNotExistCreateOne(appDirectory);
        return appDirectory;
    }

    private void IfFolderNotExistCreateOne(string directory)
    {
        if (!string.IsNullOrEmpty(directory)) 
        { 
            Directory.CreateDirectory(directory); 
        }
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
        lock (_lockFile)
        {
            var appSettingFilePath = GetSettingFilePath();
            File.WriteAllText(appSettingFilePath, JsonConvert.SerializeObject(_appSetting));
        }
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
        DecrypFiles();
        DecrypFolders();
    }

    private void DecrypFolders()
    {
        lock (_lockEncryption)
        {
            var encrypFolderPath = GetEncrypFolderPath();
            var encrypFileName = _encryptedFoldersFileName;
            var encrypFilePath = Path.Combine(encrypFolderPath, encrypFileName);
            if(!File.Exists(encrypFilePath))
                return;
            Zip.UnzipFile(encrypFilePath);
            CleanupEncryptedFolderFile();
        }
    }

    private void DecrypFiles()
    {
        lock (_lockEncryption)
        {
            var encrypFolderPath = GetEncrypFolderPath();
            var encrypFileName = _encryptedFilesFileName;
            var encrypFilePath = Path.Combine(encrypFolderPath, encrypFileName);
            if(!File.Exists(encrypFilePath))
                return;
            Zip.UnzipFile(encrypFilePath);
            CleanupEncryptedFilesFile();
        }
    }

    private void ProcessEncryption()
    {
        EncrypFiles();
        EncrypFolders();
    }

    private void EncrypFolders()
    {
        var foldersToEncryp = GetAppSetting().EncryptionTargets.Where(t => t.Category == EnumTargetCategory.Folder).Select(t => t.Path).ToList();
        lock (_lockEncryption)
        {
            if (!foldersToEncryp.Any()) return;
            CheckIfCanAccessPaths(foldersToEncryp);
            try
            {
                var encrypFolderPath = GetEncrypFolderPath();
                var encrypFileName = _encryptedFoldersFileName;
                Zip.ZipFolders(foldersToEncryp, encrypFolderPath, encrypFileName);
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
        var settingPath = GetSettingPath();
        var encrypFolderName = "Encryption";
        var encrypFolderPath = Path.Combine(settingPath, encrypFolderName);
        IfFolderNotExistCreateOne(encrypFolderPath);
        return encrypFolderPath;
    }

    private void EncrypFiles()
    {
        var filesToEncryp = GetAppSetting().EncryptionTargets.Where(t => t.Category == EnumTargetCategory.File).Select(t => t.Path).ToList();
        lock (_lockEncryption)
        {
            if (!filesToEncryp.Any()) return;
            CheckIfCanReadFiles(filesToEncryp);
            try
            {
                var encrypFolderPath = GetEncrypFolderPath();
                var encrypFileName = _encryptedFilesFileName;
                Zip.ZipFiles(filesToEncryp, encrypFolderPath, encrypFileName);
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
