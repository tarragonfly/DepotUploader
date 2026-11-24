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

    public string RemovedGameScriptsFolder => Path.Combine(ScriptsFolder, "removed");

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

    public List<string> AppScriptAndDepotScriptsOnlyItUses(string appScriptPath, string appId)
    {
        BuildScriptFile appScript = BuildScriptFile.Read(appScriptPath);
        if (appScript.Root.GetValue("appid") != appId)
        {
            throw new InvalidOperationException($"{Path.GetFileName(appScriptPath)} changed on disk after it was "
                + "loaded. Click Reload, then remove the game again.");
        }

        List<string> scriptPaths = new List<string> { appScriptPath };
        string appScriptFullPath = Path.GetFullPath(appScriptPath);
        HashSet<string> depotScriptsOfOtherScripts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string otherScriptPath in Directory.GetFiles(ScriptsFolder, "*.vdf"))
            {
                if (!string.Equals(Path.GetFullPath(otherScriptPath), appScriptFullPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    depotScriptsOfOtherScripts.UnionWith(DepotScriptPathsIn(BuildScriptFile.Read(otherScriptPath)));
                }
            }
        }
        catch (Exception exception) when (exception is FormatException or IOException
            or UnauthorizedAccessException)
        {
            return scriptPaths;
        }

        if (depotScriptsOfOtherScripts.Contains(appScriptFullPath))
        {
            throw new InvalidOperationException($"{Path.GetFileName(appScriptPath)} is a depot script of another "
                + "script in the scripts folder.");
        }

        scriptPaths.AddRange(DepotScriptPathsIn(appScript)
            .Where(depotScriptPath => !depotScriptsOfOtherScripts.Contains(depotScriptPath)
                && IsDepotScriptInScriptsFolder(depotScriptPath))
            .Distinct(StringComparer.OrdinalIgnoreCase));

        return scriptPaths;
    }

    private static IEnumerable<string> DepotScriptPathsIn(BuildScriptFile scriptFile)
    {
        List<KeyValuesNode>? depotEntries = scriptFile.Root.FindChild("depots")?.Children;

        return depotEntries is null
            ? Enumerable.Empty<string>()
            : depotEntries.Where(depotEntry => depotEntry.Value is not null)
                .Select(depotEntry => scriptFile.AbsolutePath(depotEntry.Value!));
    }

    private bool IsDepotScriptInScriptsFolder(string scriptPath)
    {
        try
        {
            return string.Equals(Path.GetDirectoryName(scriptPath), Path.GetFullPath(ScriptsFolder),
                    StringComparison.OrdinalIgnoreCase)
                && File.Exists(scriptPath)
                && BuildScriptFile.Read(scriptPath).Root.GetValue("DepotID").Length > 0;
        }
        catch (Exception exception) when (exception is FormatException or IOException
            or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    public void MoveToRemovedGameScriptsFolder(IReadOnlyList<string> scriptPaths)
    {
        foreach (string scriptPath in scriptPaths)
        {
            if (AllOrNothingFileWrite.PathExists(PathInRemovedGameScriptsFolder(scriptPath)))
            {
                throw new InvalidOperationException($"{RemovedGameScriptsFolder} already has a "
                    + $"{Path.GetFileName(scriptPath)}. Move or delete that file, then remove the game again.");
            }
        }

        Directory.CreateDirectory(RemovedGameScriptsFolder);
        List<string> movedScriptPaths = new List<string>();
        try
        {
            foreach (string scriptPath in scriptPaths)
            {
                File.Move(scriptPath, PathInRemovedGameScriptsFolder(scriptPath));
                movedScriptPaths.Add(scriptPath);
            }
        }
        catch (Exception moveError) when (moveError is IOException or UnauthorizedAccessException)
        {
            List<string> scriptNamesNotMovedBack = new List<string>();
            foreach (string movedScriptPath in movedScriptPaths)
            {
                try
                {
                    File.Move(PathInRemovedGameScriptsFolder(movedScriptPath), movedScriptPath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    scriptNamesNotMovedBack.Add(Path.GetFileName(movedScriptPath));
                }
            }

            if (scriptNamesNotMovedBack.Count > 0)
            {
                throw new IOException($"{moveError.Message} {string.Join(", ", scriptNamesNotMovedBack)} could not "
                    + $"be moved back and is in {RemovedGameScriptsFolder}.", moveError);
            }

            throw;
        }
    }

    private string PathInRemovedGameScriptsFolder(string scriptPath)
    {
        return Path.Combine(RemovedGameScriptsFolder, Path.GetFileName(scriptPath));
    }
}
