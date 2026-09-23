using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calypsov.Models;

/// <summary>Serializes <see cref="EnumTargetCategory"/> as "folder"/"file" over HTTP, matching the frontend's contract.</summary>
public sealed class EnumTargetCategoryJsonConverter : JsonConverter<EnumTargetCategory>
{
    public override EnumTargetCategory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString()?.Trim().ToLowerInvariant();
        return value switch
        {
            "folder" => EnumTargetCategory.Folder,
            "file" => EnumTargetCategory.File,
            _ => throw new JsonException("Category must be \"folder\" or \"file\"."),
        };
    }

    public override void Write(Utf8JsonWriter writer, EnumTargetCategory value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            EnumTargetCategory.Folder => "folder",
            EnumTargetCategory.File => "file",
            _ => throw new JsonException($"Unknown {nameof(EnumTargetCategory)} value: {value}."),
        });
    }
}
