using System.Runtime.InteropServices;
using Calypsov.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

    /// <summary>
    /// Reads Edge's "Local State" file for the profile list (name + folder per profile), then
    /// looks in each profile's own folder for a cached account picture to use as the avatar.
    /// Returns an empty list (rather than throwing) if Edge isn't installed or the file is
    /// missing/malformed — that's a normal "no profiles to show" case, not an error.
    /// </summary>
    private List<BrowserProfile> GetMsEdgeBrowserProfiles()
    {
        var userDataDirectory = GetEdgeUserDataDirectory();
        var localStatePath = Path.Combine(userDataDirectory, "Local State");
        if (!File.Exists(localStatePath))
            return [];

        JObject localState;
        try
        {
            localState = JObject.Parse(File.ReadAllText(localStatePath));
        }
        catch (JsonException)
        {
            return [];
        }

        if (localState["profile"]?["info_cache"] is not JObject infoCache)
            return [];

        var profiles = new List<BrowserProfile>();
        foreach (var entry in infoCache.Properties())
        {
            // The property name is the profile's folder name under the user-data
            // directory (e.g. "Default", "Profile 1") — also a stable, unique id.
            var folderName = entry.Name;
            if (entry.Value is not JObject info)
                continue;

            var name = info["name"]?.Value<string>() ?? folderName;
            var profileFolderPath = Path.Combine(userDataDirectory, folderName);
            var avatarUrl = TryReadAvatarAsDataUri(profileFolderPath);

            profiles.Add(new BrowserProfile(folderName, name, avatarUrl, profileFolderPath));
        }

        return profiles;
    }

    private string GetEdgeUserDataDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Microsoft", "Edge", "User Data");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "Microsoft Edge");
        }

        // Linux
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDirectory, ".config", "microsoft-edge");
    }

    /// <summary>
    /// Chromium-based browsers (Edge included) cache a downloaded account picture as an image
    /// file directly in the profile's own folder under one of these names, depending on version.
    /// </summary>
    private static readonly string[] KnownAvatarFileNames =
    [
        "Google Profile Picture.png",
        "Edge Profile Picture.png",
    ];

    /// <summary>
    /// Encodes a cached avatar file as a data URI so the frontend can use it directly as an
    /// &lt;img&gt; src with no extra endpoint needed to serve local files. Returns null if no
    /// cached picture is found — the frontend falls back to showing the profile's initials.
    /// </summary>
    private static string? TryReadAvatarAsDataUri(string profileFolderPath)
    {
        foreach (var fileName in KnownAvatarFileNames)
        {
            var path = Path.Combine(profileFolderPath, fileName);
            if (!File.Exists(path))
                continue;

            try
            {
                var bytes = File.ReadAllBytes(path);
                var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
                var mimeType = extension is "jpg" or "jpeg" ? "image/jpeg" : "image/png";
                return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
            }
            catch (IOException)
            {
                return null;
            }
        }

        return null;
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
