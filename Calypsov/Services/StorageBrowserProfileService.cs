using Calypsov.Models;
using Newtonsoft.Json;

namespace Calypsov.Services;

public class StorageBrowserProfileService : IBrowserProfileService
{
    private Dictionary<BrowserType, string?>? _selections;
    private readonly object _lock = new();
    private readonly object _lockFile = new();
    private const string SettingsFileName = "browserProfiles.json";

    public IReadOnlyList<BrowserProfile> GetProfiles(BrowserType browser)
    {
        switch (browser)
        {
            case BrowserType.Edge:
            return GetMsEdgeBrowserProfiles();
            case BrowserType.Chrome:
            return GetChromeBrowserProfiles();
            default:
            return [];
        }
    }

    private List<BrowserProfile> GetChromeBrowserProfiles()
    {
       return [];
    }

    private List<BrowserProfile> GetMsEdgeBrowserProfiles()
    {
        
    }

    public string? GetSelectedProfileId(BrowserType browser)
    {
        lock (_lock)
        {
            var selections = GetSelections();
            return selections.TryGetValue(browser, out var profileId) ? profileId : null;
        }
    }

    public void SetSelectedProfileId(BrowserType browser, string? profileId)
    {
        lock (_lock)
        {
            var selections = GetSelections();
            selections[browser] = profileId;
            SaveSelections();
        }
    }

    private Dictionary<BrowserType, string?> GetSelections()
    {
        if (_selections == null)
        {
            LoadSelections();
        }
        return _selections!;
    }

    private void LoadSelections()
    {
        lock (_lockFile)
        {
            try
            {
                var settingsPath = GetSettingsFilePath();
                var json = File.ReadAllText(settingsPath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    _selections = new Dictionary<BrowserType, string?>();
                    return;
                }

                _selections = JsonConvert.DeserializeObject<Dictionary<BrowserType, string?>>(json)
                    ?? new Dictionary<BrowserType, string?>();
            }
            catch
            {
                _selections = new Dictionary<BrowserType, string?>();
            }
        }
    }

    private void SaveSelections()
    {
        lock (_lockFile)
        {
            var settingsPath = GetSettingsFilePath();
            File.WriteAllText(settingsPath, JsonConvert.SerializeObject(_selections));
        }
    }

    private string GetSettingsFilePath()
    {
        var appDirectory = GetSettingPath();
        var settingsPath = Path.Combine(appDirectory, SettingsFileName);
        if (!File.Exists(settingsPath))
        {
            using var stream = File.Create(settingsPath);
        }
        return settingsPath;
    }

    private string GetSettingPath()
    {
        var appFolderName = "Calypsov";
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDirectory = Path.Combine(appData, appFolderName);
        Directory.CreateDirectory(appDirectory);
        return appDirectory;
    }
}
