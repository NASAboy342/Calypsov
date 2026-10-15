namespace Calypsov.Models;

/// <summary>
/// A named, independently-configured set of encryption targets (folders, files, and selected
/// browser profiles) the user can switch between. Exactly one profile is ever "current" at a time —
/// only the current profile's targets/browser-profile selections are affected by
/// AddTarget/RemoveTarget/browser-profile-selection, and only the current profile can ever have
/// IsEncrypted true (creating, switching, or deleting profiles is blocked while the current profile
/// is encrypted or an encrypt/decrypt run is in progress).
/// </summary>
public class ProtectionProfile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";

    // Color/Icon are opaque string tokens chosen from the frontend's own fixed palette/icon set —
    // this service only stores and returns them (validated as non-empty), since the actual palette
    // is a frontend design decision, not a backend one.
    public string Color { get; set; } = "";
    public string Icon { get; set; } = "";

    public bool IsEncrypted { get; set; }
    public List<EncryptionTarget> EncryptionTargets { get; set; } = new();
    public Dictionary<BrowserType, string?> BrowserProfileSelections { get; set; } = new();
}
