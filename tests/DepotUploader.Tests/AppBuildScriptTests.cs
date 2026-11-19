namespace DepotUploader.Tests;

public class AppBuildScriptTests
{
    [Test]
    public void Save_RestoresTheAppScriptWhenADepotScriptCannotBeReplaced()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\old");
        string appTextBefore = File.ReadAllText(appScriptPath);
        AppBuildScript game = AppBuildScript.Load(appScriptPath);
        game.Description = "1.1";
        game.Depots[0].ContentFolder = "E:\\new";

        using (new FileStream(game.Depots[0].FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.That(() => game.Save(), Throws.InstanceOf<IOException>());
        }

        Assert.That(File.ReadAllText(appScriptPath), Is.EqualTo(appTextBefore));
        Assert.That(Directory.GetFiles(folder.ScriptsPath), Has.Length.EqualTo(2));

        game.Save();

        AppBuildScript reread = AppBuildScript.Load(appScriptPath);
        Assert.That(reread.Description, Is.EqualTo("1.1"));
        Assert.That(reread.Depots[0].ContentFolder, Is.EqualTo("E:\\new"));
        Assert.That(Directory.GetFiles(folder.ScriptsPath), Has.Length.EqualTo(2));
    }

    [Test]
    public void Save_RefusesWhenAScriptChangedOnDiskAfterLoading()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\old");
        string appTextBefore = File.ReadAllText(appScriptPath);
        AppBuildScript game = AppBuildScript.Load(appScriptPath);
        string depotScriptPath = game.Depots[0].FilePath;
        string depotTextFromAnotherProgram = File.ReadAllText(depotScriptPath)
            .Replace("\"FileExclusion\" \"*.pdb\"", "\"FileExclusion\" \"*.pdb\"\r\n\t\"FileExclusion\" \"*.log\"");
        File.WriteAllText(depotScriptPath, depotTextFromAnotherProgram);

        game.Description = "1.1";

        Assert.That(() => game.Save(), Throws.InstanceOf<IOException>().With.Message.Contains("depot_100011.vdf"));
        Assert.That(File.ReadAllText(depotScriptPath), Is.EqualTo(depotTextFromAnotherProgram));
        Assert.That(File.ReadAllText(appScriptPath), Is.EqualTo(appTextBefore));
    }

    [Test]
    public void Save_WritesOnlyTheScriptsWithEdits()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\old");
        AppBuildScript game = AppBuildScript.Load(appScriptPath);
        string depotTextBefore = File.ReadAllText(game.Depots[0].FilePath);

        game.Description = "1.1";
        game.Depots[0].ContentFolder = "E:\\old";
        game.Save();

        Assert.That(AppBuildScript.Load(appScriptPath).Description, Is.EqualTo("1.1"));
        Assert.That(File.ReadAllText(game.Depots[0].FilePath), Is.EqualTo(depotTextBefore));
    }

    [Test]
    public void AbsoluteContentFolder_UsesTheDepotFolderThenTheAppFolder()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        AppBuildScript game = AppBuildScript.Load(WriteScriptsWithRelativePaths(folder));

        Assert.That(game.AbsoluteContentFolder(game.Depots[0]),
            Is.EqualTo(Path.Combine(folder.ContentBuilderPath, "content")));
        Assert.That(game.AbsoluteContentFolder(game.Depots[1]), Is.EqualTo(Path.Combine(folder.ScriptsPath, "windows")));
        Assert.That(game.AbsoluteContentFolder(game.Depots[2]), Is.EqualTo("E:\\builds\\linux"));
        Assert.That(game.AbsoluteContentFolder(game.Depots[2], string.Empty),
            Is.EqualTo(Path.Combine(folder.ContentBuilderPath, "content")));
    }

    [Test]
    public void AbsoluteContentFolder_IsEmptyWhenNeitherScriptSetsAFolder()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        AppBuildScript game = AppBuildScript.Load(
            folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", string.Empty));

        Assert.That(game.AbsoluteContentFolder(game.Depots[0]), Is.Empty);
    }

    [Test]
    public void WriteCopiesWithAbsolutePaths_WritesScriptsThatDoNotDependOnTheScriptsFolder()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = WriteScriptsWithRelativePaths(folder);
        string appTextBefore = File.ReadAllText(appScriptPath);
        AppBuildScript game = AppBuildScript.Load(appScriptPath);
        string copiesFolder = Path.Combine(folder.ContentBuilderPath, "copies");

        string appCopyPath = game.WriteCopiesWithAbsolutePaths(copiesFolder);

        KeyValuesNode appCopy = KeyValuesText.Parse(File.ReadAllText(appCopyPath));
        Assert.That(appCopy.GetValue("buildoutput"), Is.EqualTo(Path.Combine(folder.ContentBuilderPath, "output")));
        Assert.That(appCopy.GetValue("contentroot"), Is.EqualTo(Path.Combine(folder.ContentBuilderPath, "content")));
        List<KeyValuesNode> depotCopyPaths = appCopy.FindChild("depots")!.Children!;
        Assert.That(depotCopyPaths.Select(depot => depot.Key), Is.EqualTo(new[] { "1", "2", "3" }));
        Assert.That(depotCopyPaths.Select(depot => Path.GetDirectoryName(depot.Value)), Is.All.EqualTo(copiesFolder));
        Assert.That(depotCopyPaths.Select(depot => KeyValuesText.Parse(File.ReadAllText(depot.Value!)).GetValue("contentroot")),
            Is.EqualTo(new[]
            {
                Path.Combine(folder.ContentBuilderPath, "content"),
                Path.Combine(folder.ScriptsPath, "windows"),
                "E:\\builds\\linux",
            }));
        Assert.That(KeyValuesText.Parse(File.ReadAllText(depotCopyPaths[2].Value!)).GetValue("FileExclusion"),
            Is.EqualTo("*.pdb"));
        Assert.That(File.ReadAllText(appScriptPath), Is.EqualTo(appTextBefore));
    }

    [Test]
    public void WriteCopiesWithAbsolutePaths_RefusesADepotWithoutAContentFolder()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        AppBuildScript game = AppBuildScript.Load(
            folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", string.Empty));

        Assert.That(() => game.WriteCopiesWithAbsolutePaths(Path.Combine(folder.ContentBuilderPath, "copies")),
            Throws.TypeOf<InvalidOperationException>());
    }

    private static string WriteScriptsWithRelativePaths(TemporaryContentBuilderFolder folder)
    {
        folder.WriteScript("depot_1.vdf", "\"DepotBuildConfig\" { \"DepotID\" \"1\" }");
        folder.WriteScript("depot_2.vdf", "\"DepotBuildConfig\" { \"DepotID\" \"2\" \"ContentRoot\" \"windows\" }");
        folder.WriteScript("depot_3.vdf",
            "\"DepotBuildConfig\" { \"DepotID\" \"3\" \"ContentRoot\" \"E:\\builds\\linux\" \"FileExclusion\" \"*.pdb\" }");

        return folder.WriteScript("app_100030.vdf",
            "\"AppBuild\" { \"AppID\" \"100030\"\"ContentRoot\" \"..\\content\" \"BuildOutput\" \"..\\output\" "
            + "\"Depots\" { \"1\" \"depot_1.vdf\" \"2\" \"depot_2.vdf\" \"3\" \"depot_3.vdf\" } }");
    }
}
