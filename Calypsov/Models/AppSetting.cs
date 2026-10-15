using System;

namespace Calypsov.Models;

public class AppSetting
{
    public List<ProtectionProfile> Profiles { get; set; } = new();
    public Guid CurrentProfileId { get; set; }

    public bool IsUseTurboZip {get; set;}
    public bool IsEncryptZippedFile {get; set;}
    public bool IsRecordLog {get; set;}
    public bool IsEncryptAppSetting {get; set;} = true;

    // --- Legacy, pre-profiles fields. Only ever read once, by
    // StorageEncryptionSettingsService.MigrateToProfilesIfNeeded() the first time a settings.json
    // written before protection profiles existed is loaded - never written to again afterward. Kept
    // here (rather than removed) purely so Newtonsoft can still deserialize an old file's values. ---
    public List<EncryptionTarget>? EncryptionTargets { get; set; }
    public bool IsEncrypted { get; set; }
}
