using System.Text.RegularExpressions;

namespace DepotUploader;

internal sealed class ContentBuilderFolder
{
    private static readonly Regex AppScriptFileName = new Regex(@"^app_\d+\.vdf$", RegexOptions.IgnoreCase);

    public ContentBuilderFolder(string folderPath)
    {
        FolderPath = folderPath;
    }

    public string FolderPath { get; }

    public string ScriptsFolder => Path.Combine(FolderPath, "scripts");

    public string BuildOutputFolder => Path.Combine(FolderPath, "output");

    public string SteamCmdPath => Path.Combine(FolderPath, "builder", "steamcmd.exe");

    public string AppBuildLogPath(string appId)
    {
        return Path.Combine(BuildOutputFolder, $"app_build_{appId}.log");
    }

    public List<AppBuildScript> LoadAppBuildScripts(out List<string> scriptLoadErrors)
    {
        List<AppBuildScript> scripts = new List<AppBuildScript>();
        scriptLoadErrors = new List<string>();
        if (!Directory.Exists(ScriptsFolder))
        {
            return scripts;
        }

        string[] files = Directory.GetFiles(ScriptsFolder, "app_*.vdf");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            if (!AppScriptFileName.IsMatch(Path.GetFileName(file)))
            {
                continue;
            }

            try
            {
                scripts.Add(AppBuildScript.Load(file));
            }
            catch (Exception exception) when (exception is FormatException or IOException
                or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                scriptLoadErrors.Add($"{Path.GetFileName(file)}: {exception.Message}");
            }
        }

        return scripts;
    }

    public AppBuildScript CreateAppBuildScript(string appId, IReadOnlyList<string> depotIds)
    {
        string appScriptPath = Path.Combine(ScriptsFolder, $"app_{appId}.vdf");
        if (File.Exists(appScriptPath))
        {
            throw new InvalidOperationException($"app_{appId}.vdf already exists.");
        }

        List<DepotBuildScript> depots = new List<DepotBuildScript>();
        foreach (string depotId in depotIds)
        {
            string depotScriptPath = Path.Combine(ScriptsFolder, $"depot_{depotId}.vdf");
            if (File.Exists(depotScriptPath))
            {
                throw new InvalidOperationException($"depot_{depotId}.vdf already exists.");
            }

            depots.Add(DepotBuildScript.CreateForWholeFolder(depotScriptPath, depotId, string.Empty));
        }

        Directory.CreateDirectory(ScriptsFolder);
        AppBuildScript script = AppBuildScript.Create(appScriptPath, appId, BuildOutputFolder, depots);
        script.Save();

        return script;
    }
}
