namespace DepotUploader;

internal sealed class AppBuildScript
{
    private readonly BuildScriptFile _scriptFile;
    private readonly List<DepotBuildScript> _depots;

    private AppBuildScript(BuildScriptFile scriptFile, List<DepotBuildScript> depots)
    {
        _scriptFile = scriptFile;
        _depots = depots;
    }

    public string FilePath => _scriptFile.FilePath;

    public IReadOnlyList<DepotBuildScript> Depots => _depots;

    public string AppId => _scriptFile.Root.GetValue("appid");

    public string Description
    {
        get => _scriptFile.Root.GetValue("desc");
        set => _scriptFile.Root.SetValueWhenDifferent("desc", value);
    }

    public string LiveBranch
    {
        get => _scriptFile.Root.GetValue("setlive");
        set => _scriptFile.Root.SetValueWhenDifferent("setlive", value);
    }

    public bool UploadsNothing
    {
        get => _scriptFile.Root.GetValue("preview") == "1";
        set
        {
            if (value != UploadsNothing)
            {
                _scriptFile.Root.SetValue("preview", value ? "1" : "0");
            }
        }
    }

    public static AppBuildScript Load(string filePath)
    {
        BuildScriptFile scriptFile = BuildScriptFile.Read(filePath);
        List<DepotBuildScript> depots = new List<DepotBuildScript>();
        KeyValuesNode? depotsBlock = scriptFile.Root.FindChild("depots");
        if (depotsBlock?.Children is not null)
        {
            foreach (KeyValuesNode depot in depotsBlock.Children)
            {
                if (depot.Value is null)
                {
                    throw new FormatException(
                        $"Depot {depot.Key} is written inside the app script. Only depots in their own file are supported.");
                }

                depots.Add(DepotBuildScript.Load(scriptFile.AbsolutePath(depot.Value)));
            }
        }

        return new AppBuildScript(scriptFile, depots);
    }

    public static AppBuildScript Create(string filePath, string appId, string buildOutputFolder,
        List<DepotBuildScript> depots)
    {
        KeyValuesNode root = new KeyValuesNode("appbuild");
        root.SetValue("appid", appId);
        root.SetValue("desc", string.Empty);
        root.SetValue("buildoutput", buildOutputFolder);
        root.SetValue("contentroot", string.Empty);
        root.SetValue("setlive", string.Empty);
        root.SetValue("preview", "0");
        root.SetValue("local", string.Empty);
        KeyValuesNode depotsBlock = new KeyValuesNode("depots");
        foreach (DepotBuildScript depot in depots)
        {
            depotsBlock.SetValue(depot.DepotId, depot.FilePath);
        }

        root.Children!.Add(depotsBlock);

        return new AppBuildScript(BuildScriptFile.NotYetWritten(filePath, root), depots);
    }

    public string DepotScriptPath(string depotId)
    {
        return _scriptFile.AbsolutePath(DepotScriptName(depotId));
    }

    private static string DepotScriptName(string depotId)
    {
        return $"depot_{depotId}.vdf";
    }

    public DepotBuildScript AddDepotAndSave(string depotId)
    {
        KeyValuesNode? depotsBlockInScript = _scriptFile.Root.FindChild("depots");
        if (_depots.Any(depot => depot.DepotId == depotId) || depotsBlockInScript?.FindChild(depotId) is not null)
        {
            throw new InvalidOperationException($"Depot {depotId} is already in {Path.GetFileName(FilePath)}.");
        }

        string depotScriptPath = DepotScriptPath(depotId);
        DepotBuildScript depot = File.Exists(depotScriptPath)
            ? DepotBuildScript.Load(depotScriptPath)
            : DepotBuildScript.CreateForWholeFolder(depotScriptPath, depotId, string.Empty);
        if (depot.DepotId != depotId)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(depotScriptPath)} already exists and is the script of depot {depot.DepotId}.");
        }

        KeyValuesNode depotsBlock = depotsBlockInScript ?? new KeyValuesNode("depots");
        depotsBlock.SetValue(depotId, DepotScriptName(depotId));
        if (depotsBlockInScript is null)
        {
            _scriptFile.Root.Children!.Add(depotsBlock);
        }

        _depots.Add(depot);
        try
        {
            Save();
        }
        catch
        {
            _depots.Remove(depot);
            depotsBlock.Children!.RemoveAt(depotsBlock.Children.Count - 1);
            if (depotsBlockInScript is null)
            {
                _scriptFile.Root.Children!.Remove(depotsBlock);
            }

            throw;
        }

        return depot;
    }

    public void RemoveDepotAndSave(DepotBuildScript depot)
    {
        int depotIndex = _depots.IndexOf(depot);
        List<KeyValuesNode> depotEntries = _scriptFile.Root.FindChild("depots")!.Children!;
        KeyValuesNode removedDepotEntry = depotEntries[depotIndex];
        depotEntries.RemoveAt(depotIndex);
        _depots.RemoveAt(depotIndex);
        try
        {
            Save();
        }
        catch
        {
            depotEntries.Insert(depotIndex, removedDepotEntry);
            _depots.Insert(depotIndex, depot);

            throw;
        }
    }

    public string AbsoluteContentFolder(DepotBuildScript depot)
    {
        return AbsoluteContentFolder(depot, depot.ContentFolder);
    }

    public string AbsoluteContentFolder(DepotBuildScript depot, string depotContentFolder)
    {
        if (depotContentFolder.Length > 0)
        {
            return depot.ScriptFile.AbsolutePath(depotContentFolder);
        }

        string appContentFolder = _scriptFile.Root.GetValue("contentroot");

        return appContentFolder.Length == 0 ? string.Empty : _scriptFile.AbsolutePath(appContentFolder);
    }

    public void Save()
    {
        List<BuildScriptFile> scriptFiles = Depots.Select(depot => depot.ScriptFile).ToList();
        scriptFiles.Add(_scriptFile);

        List<BuildScriptFile> editedScriptFiles = scriptFiles.Where(scriptFile => scriptFile.HasUnsavedEdits).ToList();
        List<(string FilePath, string Text)> editedScripts = editedScriptFiles
            .Select(scriptFile => (scriptFile.FilePath, scriptFile.ToScriptText())).ToList();

        foreach (BuildScriptFile scriptFile in scriptFiles)
        {
            if (scriptFile.FileChangedAfterLastReadOrWrite())
            {
                throw new IOException($"{Path.GetFileName(scriptFile.FilePath)} changed on disk after it was loaded. "
                    + "Click Reload to read it again. Reload discards the edits in this window.");
            }
        }

        AllOrNothingFileWrite.WriteAll(editedScripts);
        for (int i = 0; i < editedScriptFiles.Count; i++)
        {
            editedScriptFiles[i].RememberWrittenText(editedScripts[i].Text);
        }
    }

    public string WriteCopiesWithAbsolutePaths(string folderPath)
    {
        KeyValuesNode appCopy = CopyOf(_scriptFile.Root);
        appCopy.SetValue("buildoutput", _scriptFile.AbsolutePath(appCopy.GetValue("buildoutput")));
        MakeAbsoluteWhenSet(appCopy, "contentroot");
        MakeAbsoluteWhenSet(appCopy, "local");

        Directory.CreateDirectory(folderPath);
        List<KeyValuesNode>? depotScriptPaths = appCopy.FindChild("depots")?.Children;
        for (int i = 0; i < Depots.Count; i++)
        {
            string contentFolder = AbsoluteContentFolder(Depots[i]);
            if (contentFolder.Length == 0)
            {
                throw new InvalidOperationException($"Depot {Depots[i].DepotId} has no content folder.");
            }

            KeyValuesNode depotCopy = CopyOf(Depots[i].ScriptFile.Root);
            depotCopy.SetValue("contentroot", contentFolder);
            string depotCopyPath = Path.Combine(folderPath, $"depot_build_{i + 1}.vdf");
            File.WriteAllText(depotCopyPath, KeyValuesText.Write(depotCopy));
            depotScriptPaths![i].Value = depotCopyPath;
        }

        string appCopyPath = Path.Combine(folderPath, "app_build.vdf");
        File.WriteAllText(appCopyPath, KeyValuesText.Write(appCopy));

        return appCopyPath;
    }

    private static KeyValuesNode CopyOf(KeyValuesNode root)
    {
        return KeyValuesText.Parse(KeyValuesText.Write(root));
    }

    private void MakeAbsoluteWhenSet(KeyValuesNode appCopy, string pathKey)
    {
        string path = appCopy.GetValue(pathKey);
        if (path.Length > 0)
        {
            appCopy.SetValue(pathKey, _scriptFile.AbsolutePath(path));
        }
    }
}
