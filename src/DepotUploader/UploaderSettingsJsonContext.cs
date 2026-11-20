using System.Text.Json.Serialization;

namespace DepotUploader;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(UploaderSettings))]
internal sealed partial class UploaderSettingsJsonContext : JsonSerializerContext;
