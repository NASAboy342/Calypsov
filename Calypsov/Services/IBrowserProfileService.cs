using Calypsov.Models;

namespace Calypsov.Services;

/// <summary>
/// Detects installed browser profiles and stores which one the user has picked per browser.
/// </summary>
public interface IBrowserProfileService
{
    /// <summary>Lists the profiles installed for the given browser.</summary>
    IReadOnlyList<BrowserProfile> GetProfiles(BrowserType browser);
    Dictionary<BrowserType,BrowserProfile> GetSelectedProfile();

    /// <summary>The currently selected profile id for the browser, or null for "None" (the default).</summary>
    string? GetSelectedProfileId(BrowserType browser);

    /// <summary>Sets the selected profile id for the browser; null selects "None".</summary>
    void SetSelectedProfileId(BrowserType browser, string? profileId);
}
