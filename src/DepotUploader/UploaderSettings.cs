namespace DepotUploader;

internal sealed class UploaderSettings
{
    public string ContentBuilderPath { get; set; } = string.Empty;

    public string SteamLogin { get; set; } = string.Empty;

    public Dictionary<string, string> GameNamesByAppId { get; set; } = new Dictionary<string, string>();

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

            return FromJson(text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or FormatException)
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

        AllOrNothingFileWrite.WriteAll(new[] { (filePath, ToIndentedJson()) });
    }

    public string GameName(string appId)
    {
        return GameNamesByAppId.TryGetValue(appId, out string? name) && !string.IsNullOrEmpty(name)
            ? name
            : $"App {appId}";
    }

    private static UploaderSettings FromJson(string text)
    {
        UploaderSettings settings = new UploaderSettings();
        foreach (KeyValuePair<string, object?> member in MembersOfJsonObject(JsonText.Parse(text), "The JSON text"))
        {
            switch (member.Key)
            {
                case nameof(ContentBuilderPath):
                    settings.ContentBuilderPath = JsonTextOrEmpty(member);
                    break;
                case nameof(SteamLogin):
                    settings.SteamLogin = JsonTextOrEmpty(member);
                    break;
                case nameof(GameNamesByAppId):
                    settings.GameNamesByAppId = new Dictionary<string, string>();
                    foreach (KeyValuePair<string, object?> gameName in MembersOfJsonObject(member.Value, member.Key))
                    {
                        settings.GameNamesByAppId[gameName.Key] = JsonTextOrEmpty(gameName);
                    }

                    break;
            }
        }

        return settings;
    }

    private static IEnumerable<KeyValuePair<string, object?>> MembersOfJsonObject(object? json, string name)
    {
        return json switch
        {
            null => Enumerable.Empty<KeyValuePair<string, object?>>(),
            List<KeyValuePair<string, object?>> members => members,
            _ => throw new FormatException($"{name} is not a JSON object."),
        };
    }

    private static string JsonTextOrEmpty(KeyValuePair<string, object?> member)
    {
        return member.Value switch
        {
            null => string.Empty,
            string text => text,
            _ => throw new FormatException($"{member.Key} is not text."),
        };
    }

    private string ToIndentedJson()
    {
        IEnumerable<string> gameNameLines = GameNamesByAppId.Select(gameName =>
            $"    {JsonText.Quote(gameName.Key)}: {JsonText.Quote(gameName.Value ?? string.Empty)}");
        string gameNames = GameNamesByAppId.Count == 0
            ? "{}"
            : "{" + Environment.NewLine + string.Join("," + Environment.NewLine, gameNameLines)
            + Environment.NewLine + "  }";

        return string.Join(Environment.NewLine,
            "{",
            $"  \"{nameof(ContentBuilderPath)}\": {JsonText.Quote(ContentBuilderPath)},",
            $"  \"{nameof(SteamLogin)}\": {JsonText.Quote(SteamLogin)},",
            $"  \"{nameof(GameNamesByAppId)}\": {gameNames}",
            "}");
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
}
