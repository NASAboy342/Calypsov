using Newtonsoft.Json;

namespace Calypsov.Repositories;

/// <summary>
/// The app-data folder resolution here (<see cref="GetSettingPath"/>) used to be copy-pasted,
/// line for line, into both <c>StorageEncryptionSettingsService</c> and
/// <c>StorageBrowserProfileService</c> — this class is that shared logic pulled out once
/// (kept as it was in <c>StorageEncryptionSettingsService</c>), plus the generic JSON load/save
/// each of those services also duplicated around it.
/// </summary>
public class SettingRepository : ISettingRepository
{
    private readonly object _lockFile = new();

    public T? Load<T>(string fileName) where T : class
    {
        lock (_lockFile)
        {
            try
            {
                var filePath = GetSettingFilePath(fileName);
                var json = File.ReadAllText(filePath);
                return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return null;
            }
        }
    }

    public void Save<T>(string fileName, T data) where T : class
    {
        lock (_lockFile)
        {
            var filePath = GetSettingFilePath(fileName);
            File.WriteAllText(filePath, JsonConvert.SerializeObject(data));
        }
    }

    public void AppendText(string folderName, string fileName, string content)
    {
        lock (_lockFile)
        {
            var filePath = Path.Combine(GetSubFolderPath(folderName), fileName);
            File.AppendAllText(filePath, content);
        }
    }

    public string? ReadText(string folderName, string fileName)
    {
        lock (_lockFile)
        {
            var filePath = Path.Combine(GetSubFolderPath(folderName), fileName);
            return File.Exists(filePath) ? File.ReadAllText(filePath) : null;
        }
    }

    public IReadOnlyList<FileEntry> ListFiles(string folderName)
    {
        lock (_lockFile)
        {
            var folderPath = GetSubFolderPath(folderName);
            return Directory.GetFiles(folderPath)
                .Select(filePath => new FileEntry(Path.GetFileName(filePath), File.GetLastWriteTimeUtc(filePath)))
                .ToList();
        }
    }

    public void DeleteFile(string folderName, string fileName)
    {
        lock (_lockFile)
        {
            var filePath = Path.Combine(GetSubFolderPath(folderName), fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private string GetSubFolderPath(string folderName)
    {
        var folderPath = Path.Combine(GetAppDataFolder(), folderName);
        IfFolderNotExistCreateOne(folderPath);
        return folderPath;
    }

    private string GetSettingFilePath(string fileName)
    {
        var appDirectory = GetAppDataFolder();
        var settingsPath = Path.Combine(appDirectory, fileName);
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

    public string GetAppDataFolder()
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
}
