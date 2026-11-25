namespace DepotUploader.Tests;

internal sealed class TemporaryContentBuilderFolder : IDisposable
{
    public TemporaryContentBuilderFolder()
    {
        ContentBuilderPath = Path.Combine(Path.GetTempPath(), "DepotUploaderTests", Guid.NewGuid().ToString("N"));
        ScriptsPath = Path.Combine(ContentBuilderPath, "scripts");
        Directory.CreateDirectory(ScriptsPath);
    }

    public string ContentBuilderPath { get; }

    public string ScriptsPath { get; }

    public string WriteScript(string fileName, string text)
    {
        string path = Path.Combine(ScriptsPath, fileName);
        File.WriteAllText(path, text);

        return path;
    }

    public string WriteAppAndDepotScriptsLikeSteamPipeGui(string appId, string depotId, string contentFolder)
    {
        string depotPath = WriteScript($"depot_{depotId}.vdf",
            "\"DepotBuildConfig\"\r\n{\r\n"
            + $"\t\"DepotID\" \"{depotId}\"\r\n"
            + $"\t\"contentroot\" \"{contentFolder}\"\r\n"
            + "\t\"FileMapping\"\r\n\t{\r\n"
            + "\t\t\"LocalPath\" \"*\"\r\n\t\t\"DepotPath\" \".\"\r\n\t\t\"recursive\" \"1\"\r\n\t}\r\n"
            + "\t\"FileExclusion\" \"*.pdb\"\r\n}");

        return WriteScript($"app_{appId}.vdf",
            "\"appbuild\"\r\n{\r\n"
            + $"\t\"appid\" \"{appId}\"\r\n"
            + "\t\"desc\" \"1.0 hotfix 1\"\r\n"
            + $"\t\"buildoutput\" \"{Path.Combine(ContentBuilderPath, "output")}\"\r\n"
            + "\t\"contentroot\" \"\"\r\n\t\"setlive\" \"\"\r\n\t\"preview\" \"0\"\r\n\t\"local\"\t\"\"\r\n"
            + "\t\"depots\"\r\n\t{\r\n"
            + $"\t\t\"{depotId}\"\t\"{depotPath}\"\r\n"
            + "\t}\r\n}");
    }

    public string ContentFolderPath(string appId)
    {
        return Path.Combine(ContentBuilderPath, "content", appId);
    }

    public string WriteContentFolderWithTwoFiles(string appId)
    {
        string contentFolder = ContentFolderPath(appId);
        Directory.CreateDirectory(contentFolder);
        foreach (string fileName in new[] { "game.exe", "data.pak" })
        {
            string filePath = Path.Combine(contentFolder, fileName);
            File.WriteAllText(filePath, "x");
            File.SetLastWriteTimeUtc(filePath, new DateTime(2026, 5, 1, 12, 0, 10, DateTimeKind.Utc));
        }

        return contentFolder;
    }

    public void Dispose()
    {
        Directory.Delete(ContentBuilderPath, true);
    }
}
