using System.Runtime.InteropServices;
using Calypsov.Models;
using Calypsov.Repositories;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calypsov.Services;

public class StorageBrowserProfileService : IBrowserProfileService
{
    private const string SettingsFileName = "browserProfiles.json";

    private readonly ISettingRepository _settingRepository;
    private Dictionary<BrowserType, string?>? _selections;
    private readonly object _lock = new();

    public StorageBrowserProfileService(ISettingRepository settingRepository)
    {
        _settingRepository = settingRepository;
    }

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

    private List<BrowserProfile> GetChromeBrowserProfiles() =>
        GetChromiumBrowserProfiles(GetChromeUserDataDirectory());

    private List<BrowserProfile> GetMsEdgeBrowserProfiles() =>
        GetChromiumBrowserProfiles(GetEdgeUserDataDirectory());

    /// <summary>
    /// Shared by Edge and Chrome — both are Chromium-based and lay out profiles the same way
    /// (a "Local State" file listing profile folders, one subfolder per profile). Reads the
    /// profile list (name + folder), then looks in each profile's own folder for a cached
    /// account picture to use as the avatar. Returns an empty list (rather than throwing) if the
    /// browser isn't installed or the file is missing/malformed — that's a normal "no profiles to
    /// show" case, not an error.
    /// </summary>
    private List<BrowserProfile> GetChromiumBrowserProfiles(string userDataDirectory)
    {
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

            var name = GetDisplayName(info, folderName);
            var profileFolderPath = Path.Combine(userDataDirectory, folderName);
            var avatarUrl = TryReadAvatarAsDataUri(profileFolderPath);

            profiles.Add(new BrowserProfile(folderName, name, avatarUrl, profileFolderPath));
        }

        return profiles;
    }

    /// <summary>
    /// Edge's own "name" field is just the generic "Profile 1"/"Profile 2" the browser assigned
    /// on creation — not editable-looking text a user would recognize their account by. Prefer
    /// the signed-in account's email ("user_name"), which is what actually distinguishes two
    /// profiles signed into different accounts; fall back to the generic name for a profile
    /// that isn't signed into anything.
    /// </summary>
    private static string GetDisplayName(JObject info, string folderName)
    {
        var userName = info["user_name"]?.Value<string>();
        if (!string.IsNullOrWhiteSpace(userName))
            return userName;

        return info["name"]?.Value<string>() ?? folderName;
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

    private string GetChromeUserDataDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Google", "Chrome", "User Data");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "Google", "Chrome");
        }

        // Linux
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDirectory, ".config", "google-chrome");
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
        _selections = _settingRepository.Load<Dictionary<BrowserType, string?>>(SettingsFileName)
            ?? new Dictionary<BrowserType, string?>();
    }

    private void SaveSelections()
    {
        _settingRepository.Save(SettingsFileName, _selections!);
    }

    public Dictionary<BrowserType, BrowserProfile> GetSelectedProfile()
    {
        var profiles = new Dictionary<BrowserType, BrowserProfile>();
        foreach (var selection in GetSelections())
        {
            if (selection.Value == null)
                continue;

            var selectedProfile = GetProfiles(selection.Key).FirstOrDefault(p => p.Id == selection.Value);
            if (selectedProfile != null)
            {
                profiles.Add(selection.Key, selectedProfile);
            }
        }
        return profiles;
    }
}
