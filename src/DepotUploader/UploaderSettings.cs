using System.Text.Json;
using System.Text.Json.Serialization;

namespace DepotUploader;

internal sealed class UploaderSettings
{
    public string ContentBuilderPath { get; set; } = string.Empty;

    public string SteamLogin { get; set; } = string.Empty;

    public Dictionary<string, string> GameNamesByAppId { get; set; } = new Dictionary<string, string>();

    [JsonIgnore]
    public bool MustNotOverwriteFile { get; private set; }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DepotUploader", "settings.json");

    public static UploaderSettings Load(string filePath, out string? loadError)
    {
        loadError = null;
        try
        {
            string? text = ReadTextIfPresent(filePath);
            if (text is null)
            {
                string? backupFilePath = BackupOfMissingFile(filePath);
                if (backupFilePath is null)
                {
                    return new UploaderSettings();
                }

                loadError = $"{MissingFileWithBackupText(filePath, backupFilePath)} Until then the settings start "
                    + "empty and are not saved.";

                return new UploaderSettings { MustNotOverwriteFile = true };
            }

            UploaderSettings settings = JsonSerializer.Deserialize(text,
                UploaderSettingsJsonContext.Default.UploaderSettings) ?? new UploaderSettings();
            settings.ReplaceNullValues();

            return settings;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or JsonException)
        {
            loadError = $"{filePath} could not be loaded, so the settings start empty and are not saved. "
                + $"{exception.Message} Fix or delete the file, then start Depot Uploader again.";

            return new UploaderSettings { MustNotOverwriteFile = true };
        }
    }

    public void Save(string filePath)
    {
        if (MustNotOverwriteFile)
        {
            throw new IOException(
                $"{filePath} could not be loaded when Depot Uploader started, so the settings are not saved.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        if (!AllOrNothingFileWrite.PathExists(filePath)
            && BackupOfMissingFile(filePath) is string backupFilePath)
        {
            throw new IOException(MissingFileWithBackupText(filePath, backupFilePath));
        }

        string text = JsonSerializer.Serialize(this, UploaderSettingsJsonContext.Default.UploaderSettings);
        AllOrNothingFileWrite.WriteAll(new[] { (filePath, text) });
    }

    public string GameName(string appId)
    {
        return GameNamesByAppId.TryGetValue(appId, out string? name) && !string.IsNullOrEmpty(name)
            ? name
            : $"App {appId}";
    }

    private static string? ReadTextIfPresent(string filePath)
    {
        try
        {
            return File.ReadAllText(filePath);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static string? BackupOfMissingFile(string filePath)
    {
        string folderPath = Path.GetDirectoryName(filePath)!;
        string fileName = Path.GetFileName(filePath);
        try
        {
            return Directory.EnumerateFiles(folderPath, $"{fileName}.previous")
                .Concat(Directory.EnumerateFiles(folderPath, $"{fileName}.*.previous"))
                .FirstOrDefault();
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static string MissingFileWithBackupText(string filePath, string backupFilePath)
    {
        return $"{filePath} is missing and a backup of it is kept as {backupFilePath}. Rename the backup to "
            + $"{Path.GetFileName(filePath)}, then start Depot Uploader again.";
    }

    private void ReplaceNullValues()
    {
        ContentBuilderPath ??= string.Empty;
        SteamLogin ??= string.Empty;
        GameNamesByAppId ??= new Dictionary<string, string>();
    }
}
