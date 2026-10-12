using System.Text;
using Calypsov.Models;
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
                var raw = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                // AppSetting is the only file this repository ever encodes (see IsEncryptAppSetting
                // on Save below) — other callers' files are never touched here, so their content is
                // always read back as the plain JSON they were written as.
                var json = typeof(T) == typeof(AppSetting) ? DecodeAppSettingIfNeeded(raw) : raw;
                return JsonConvert.DeserializeObject<T>(json);
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
            var json = JsonConvert.SerializeObject(data);

            // Encoding happens here, at the repository, specifically so IEncryptionSettingsService
            // (and anything else that reads/writes AppSetting) never has to know or care whether the
            // file on disk is plain JSON or encoded — it only ever works with a plain AppSetting
            // object. Only AppSetting is ever encoded, and only when its own IsEncryptAppSetting flag
            // is on at the moment of this call.
            var content = data is AppSetting { IsEncryptAppSetting: true } ? EncodeBase64(json) : json;
            File.WriteAllText(filePath, content);
        }
    }

    /// <summary>
    /// Detects whether <paramref name="raw"/> is plain JSON or this repository's Base64 encoding of
    /// it, and returns plain JSON either way. Detection — not the current IsEncryptAppSetting value —
    /// decides this: the flag only controls what happens on the *next* write, so a file written while
    /// the flag was on must still read back correctly after the flag is later turned off (and vice
    /// versa), before anything is re-saved. A file that was never encoded (including the very first
    /// read of a settings.json that predates this feature) is read as plain JSON, unchanged.
    /// </summary>
    private static string DecodeAppSettingIfNeeded(string raw) =>
        LooksLikePlainJson(raw) ? raw : DecodeBase64(raw);

    private static bool LooksLikePlainJson(string raw) => raw.TrimStart().StartsWith('{');

    private static string DecodeBase64(string encoded) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(encoded));

    private static string EncodeBase64(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

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
