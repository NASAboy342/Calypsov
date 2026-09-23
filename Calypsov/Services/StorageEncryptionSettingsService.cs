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
            appSetting.EncryptionTargets.Add(newTargetToAdd);
            SaveAppSetting();
        }
        return newTargetToAdd;
    }

    private static void CheckIfTargetAlreadyAddBefore(EnumTargetCategory category, string path, AppSetting appSetting)
    {
        if (appSetting.EncryptionTargets.Any(t => t.Category == category && t.Path.Equals(path)))
        {
            throw new Exception("EncryptionTarget already set. Will not add duplicated one");
        }
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
            File.Delete(encrypFilePath);
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
            File.Delete(encrypFilePath);
        }
    }

    private void ProcessEncryption()
    {
        EncrypFiles();
        EncrypFolders();
    }

    private void EncrypFolders()
    {
        lock (_lockEncryption)
        {
            var foldersToEncryp = GetAppSetting().EncryptionTargets.Where(t => t.Category == EnumTargetCategory.Folder).Select(t => t.Path).ToList();
            if(!foldersToEncryp.Any()) return;
            var encrypFolderPath = GetEncrypFolderPath();
            var encrypFileName = _encryptedFoldersFileName;
            Zip.ZipFolders(foldersToEncryp, encrypFolderPath, encrypFileName);
            foreach(var folderPath in foldersToEncryp)
            {
                Directory.Delete(folderPath, true);
            }
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
        lock (_lockEncryption)
        {
            var filesToEncryp = GetAppSetting().EncryptionTargets.Where(t => t.Category == EnumTargetCategory.File).Select(t => t.Path).ToList();
            if(!filesToEncryp.Any()) return;
            var encrypFolderPath = GetEncrypFolderPath();
            var encrypFileName = _encryptedFilesFileName;
            Zip.ZipFiles(filesToEncryp, encrypFolderPath, encrypFileName);
            foreach(var filePath in filesToEncryp)
            {
                File.Delete(filePath);
            }
        }
    }
}
