using System.Text.Json.Serialization;

namespace Calypsov.Models;

/// <summary>Whether an <see cref="EncryptionTarget"/> is a folder or a single file.</summary>
[JsonConverter(typeof(EnumTargetCategoryJsonConverter))]
public enum EnumTargetCategory
{
    Folder,
    File,
}
