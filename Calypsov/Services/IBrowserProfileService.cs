using Calypsov.Models;

namespace Calypsov.Services;

/// <summary>
/// Detects installed browser profiles. Which one the user has picked (per protection profile) is
/// stored separately, on <see cref="IEncryptionSettingsService"/> — see
/// <see cref="IEncryptionSettingsService.GetSelectedBrowserProfileId"/> — since a browser-profile
/// selection is itself part of a protection profile's configuration, not independent of it.
/// </summary>
public interface IBrowserProfileService
{
    /// <summary>Lists the profiles installed for the given browser.</summary>
    IReadOnlyList<BrowserProfile> GetProfiles(BrowserType browser);
}
