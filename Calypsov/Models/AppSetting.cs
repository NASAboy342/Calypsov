using System;

namespace Calypsov.Models;

public class AppSetting
{
    public List<EncryptionTarget> EncryptionTargets {get; set;} = new List<EncryptionTarget>();
    public bool IsEncrypted {get; set;}
    public bool IsUseTurboZip {get; set;}
    public bool IsEncryptZippedFile {get; set;}
    public bool IsRecordLog {get; set;}
}
