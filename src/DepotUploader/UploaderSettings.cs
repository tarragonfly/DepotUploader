using System.Text.Json;

namespace DepotUploader;

internal sealed class UploaderSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

    public string ContentBuilderPath { get; set; } = string.Empty;

    public string SteamLogin { get; set; } = string.Empty;

    public Dictionary<string, string> GameNamesByAppId { get; set; } = new Dictionary<string, string>();

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DepotUploader", "settings.json");

    public static UploaderSettings Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new UploaderSettings();
        }

        return JsonSerializer.Deserialize<UploaderSettings>(File.ReadAllText(filePath)) ?? new UploaderSettings();
    }

    public void Save(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public string GameName(string appId)
    {
        return GameNamesByAppId.TryGetValue(appId, out string? name) && name.Length > 0 ? name : $"App {appId}";
    }
}
