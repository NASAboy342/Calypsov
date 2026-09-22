using System;
using Calypsov.Models;
using Newtonsoft.Json;

namespace Calypsov.Services;

public class StorageEncryptionSettingsService : IEncryptionSettingsService
{
    private AppSetting? _appSetting;
    private readonly object _lock = new();
    private readonly object _lockFile = new();
    public EncryptionTarget AddTarget(string category, string path)
    {
        throw new NotImplementedException();
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
            var settingPath = GetSettingFilePath();
            var json = File.ReadAllText(settingPath);

            if (string.IsNullOrWhiteSpace(json))
            {
                _appSetting = new AppSetting();
                return;
            }

            _appSetting = JsonConvert.DeserializeObject<AppSetting>(json);
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
            appSetting.IsEncrypted = !appSetting.IsEncrypted;
            SaveAppSetting();
        }
        return GetAppSetting().IsEncrypted;
    }
}
