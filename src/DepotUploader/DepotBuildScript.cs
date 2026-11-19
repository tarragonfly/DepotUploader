namespace DepotUploader;

internal sealed class DepotBuildScript
{
    private DepotBuildScript(BuildScriptFile scriptFile)
    {
        ScriptFile = scriptFile;
    }

    public BuildScriptFile ScriptFile { get; }

    public string FilePath => ScriptFile.FilePath;

    public string DepotId => ScriptFile.Root.GetValue("DepotID");

    public string ContentFolder
    {
        get => ScriptFile.Root.GetValue("contentroot");
        set => ScriptFile.Root.SetValueWhenDifferent("contentroot", value);
    }

    public static DepotBuildScript Load(string filePath)
    {
        return new DepotBuildScript(BuildScriptFile.Read(filePath));
    }

    public static DepotBuildScript CreateForWholeFolder(string filePath, string depotId, string contentFolder)
    {
        KeyValuesNode root = new KeyValuesNode("DepotBuildConfig");
        root.SetValue("DepotID", depotId);
        root.SetValue("contentroot", contentFolder);
        KeyValuesNode fileMapping = new KeyValuesNode("FileMapping");
        fileMapping.SetValue("LocalPath", "*");
        fileMapping.SetValue("DepotPath", ".");
        fileMapping.SetValue("recursive", "1");
        root.Children!.Add(fileMapping);
        root.SetValue("FileExclusion", "*.pdb");

        return new DepotBuildScript(BuildScriptFile.NotYetWritten(filePath, root));
    }
}
