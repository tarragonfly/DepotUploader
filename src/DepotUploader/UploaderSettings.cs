using System.Text.Json;

namespace DepotUploader;

internal sealed class UploaderSettings
{
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

        return JsonSerializer.Deserialize(File.ReadAllText(filePath), UploaderSettingsJsonContext.Default.UploaderSettings) ?? new UploaderSettings();
    }

    public void Save(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, JsonSerializer.Serialize(this, UploaderSettingsJsonContext.Default.UploaderSettings));
    }

    public string GameName(string appId)
    {
        return GameNamesByAppId.TryGetValue(appId, out string? name) && name.Length > 0 ? name : $"App {appId}";
    }
}
