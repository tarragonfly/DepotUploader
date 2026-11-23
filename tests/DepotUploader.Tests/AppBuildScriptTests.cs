namespace DepotUploader.Tests;

public class AppBuildScriptTests
{
    [Test]
    public void Save_RestoresTheDepotScriptWhenTheAppScriptCannotBeReplaced()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\old");
        string appTextBefore = File.ReadAllText(appScriptPath);
        AppBuildScript game = AppBuildScript.Load(appScriptPath);
        string depotTextBefore = File.ReadAllText(game.Depots[0].FilePath);
        game.Description = "1.1";
        game.Depots[0].ContentFolder = "E:\\new";

        using (new FileStream(appScriptPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.That(() => game.Save(), Throws.InstanceOf<IOException>());
        }

        Assert.That(File.ReadAllText(game.Depots[0].FilePath), Is.EqualTo(depotTextBefore));
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
    public void AddDepotAndSave_WritesTheDepotScriptAndItsLineInTheAppScript()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\old");
        AppBuildScript game = AppBuildScript.Load(appScriptPath);

        DepotBuildScript addedDepot = game.AddDepotAndSave("100012");

        Assert.That(addedDepot.FilePath, Is.EqualTo(Path.Combine(folder.ScriptsPath, "depot_100012.vdf")));
        Assert.That(KeyValuesText.Parse(File.ReadAllText(appScriptPath)).FindChild("depots")!.GetValue("100012"),
            Is.EqualTo("depot_100012.vdf"));
        AppBuildScript reread = AppBuildScript.Load(appScriptPath);
        Assert.That(reread.Depots.Select(depot => depot.DepotId), Is.EqualTo(new[] { "100011", "100012" }));
        Assert.That(reread.Depots[1].ContentFolder, Is.Empty);
        Assert.That(reread.Depots[1].ScriptFile.Root.GetValue("FileExclusion"), Is.EqualTo("*.pdb"));
        Assert.That(reread.Description, Is.EqualTo("1.0 hotfix 1"));
    }

    [Test]
    public void AddDepotAndSave_WritesScriptsThatLoadAgainWhenTheContentBuilderFolderIsRelative()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\old");
        string currentDirectoryBefore = Environment.CurrentDirectory;
        Environment.CurrentDirectory = Path.GetDirectoryName(folder.ContentBuilderPath)!;
        try
        {
            ContentBuilderFolder contentBuilder = new ContentBuilderFolder(Path.GetFileName(folder.ContentBuilderPath));

            contentBuilder.LoadAppBuildScripts(out _).Single().AddDepotAndSave("100012");

            List<AppBuildScript> reloadedGames = contentBuilder.LoadAppBuildScripts(out List<string> scriptLoadErrors);
            Assert.That(scriptLoadErrors, Is.Empty);
            Assert.That(reloadedGames.Single().Depots.Select(depot => depot.DepotId),
                Is.EqualTo(new[] { "100011", "100012" }));
            Assert.That(File.Exists(Path.Combine(folder.ScriptsPath, "depot_100012.vdf")), Is.True);
        }
        finally
        {
            Environment.CurrentDirectory = currentDirectoryBefore;
        }
    }

    [Test]
    public void Save_RefusesWhenAFileAppearedAtThePathOfANewDepotScript()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = Path.Combine(folder.ScriptsPath, "app_100010.vdf");
        string depotScriptPath = Path.Combine(folder.ScriptsPath, "depot_100011.vdf");
        AppBuildScript game = AppBuildScript.Create(appScriptPath, "100010",
            Path.Combine(folder.ContentBuilderPath, "output"),
            new List<DepotBuildScript> { DepotBuildScript.CreateForWholeFolder(depotScriptPath, "100011", string.Empty) });
        string depotTextFromAnotherProgram = "\"DepotBuildConfig\" { \"DepotID\" \"100011\" \"FileExclusion\" \"*.log\" }";
        File.WriteAllText(depotScriptPath, depotTextFromAnotherProgram);

        Assert.That(() => game.Save(), Throws.InstanceOf<IOException>().With.Message.Contains("depot_100011.vdf"));
        Assert.That(File.ReadAllText(depotScriptPath), Is.EqualTo(depotTextFromAnotherProgram));
        Assert.That(File.Exists(appScriptPath), Is.False);
    }

    [Test]
    public void Save_PassesTheDepotScriptsToTheWriterBeforeTheAppScript()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = Path.Combine(folder.ScriptsPath, "app_100010.vdf");
        string depotScriptPath = Path.Combine(folder.ScriptsPath, "depot_100011.vdf");
        Directory.CreateDirectory(appScriptPath);
        Directory.CreateDirectory(depotScriptPath);
        AppBuildScript game = AppBuildScript.Create(appScriptPath, "100010",
            Path.Combine(folder.ContentBuilderPath, "output"),
            new List<DepotBuildScript> { DepotBuildScript.CreateForWholeFolder(depotScriptPath, "100011", string.Empty) });

        Assert.That(() => game.Save(),
            Throws.InstanceOf<IOException>().With.Message.EqualTo($"{depotScriptPath} is a folder."));
    }

    [Test]
    public void AddDepotAndSave_AddsADepotsBlockToAnAppScriptWithoutOne()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = folder.WriteScript("app_100010.vdf", "\"appbuild\" { \"appid\" \"100010\" }");
        AppBuildScript game = AppBuildScript.Load(appScriptPath);

        game.AddDepotAndSave("100011");

        Assert.That(AppBuildScript.Load(appScriptPath).Depots.Select(depot => depot.DepotId),
            Is.EqualTo(new[] { "100011" }));
    }

    [Test]
    public void AddDepotAndSave_UsesTheDepotScriptThatIsAlreadyInTheScriptsFolder()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\old");
        string existingDepotScriptPath = folder.WriteScript("depot_100012.vdf",
            "\"DepotBuildConfig\" { \"DepotID\" \"100012\" \"contentroot\" \"E:\\linux\" }");
        string existingDepotScriptText = File.ReadAllText(existingDepotScriptPath);
        AppBuildScript game = AppBuildScript.Load(appScriptPath);

        DepotBuildScript addedDepot = game.AddDepotAndSave("100012");

        Assert.That(addedDepot.ContentFolder, Is.EqualTo("E:\\linux"));
        Assert.That(File.ReadAllText(existingDepotScriptPath), Is.EqualTo(existingDepotScriptText));
        Assert.That(AppBuildScript.Load(appScriptPath).Depots, Has.Count.EqualTo(2));
    }

    [Test]
    public void AddDepotAndSave_RefusesADepotThatIsAlreadyInTheAppScript()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\old");
        string appTextBefore = File.ReadAllText(appScriptPath);
        AppBuildScript game = AppBuildScript.Load(appScriptPath);

        Assert.That(() => game.AddDepotAndSave("100011"), Throws.TypeOf<InvalidOperationException>());
        Assert.That(game.Depots, Has.Count.EqualTo(1));
        Assert.That(File.ReadAllText(appScriptPath), Is.EqualTo(appTextBefore));
    }

    [Test]
    public void AddDepotAndSave_RefusesAnExistingDepotScriptOfAnotherDepot()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\old");
        folder.WriteScript("depot_100012.vdf", "\"DepotBuildConfig\" { \"DepotID\" \"100099\" }");
        AppBuildScript game = AppBuildScript.Load(appScriptPath);

        Assert.That(() => game.AddDepotAndSave("100012"),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("100099"));
        Assert.That(game.Depots, Has.Count.EqualTo(1));
    }

    [Test]
    public void AddDepotAndSave_LeavesTheGameAsItWasWhenTheAppScriptCannotBeReplaced()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\old");
        string appTextBefore = File.ReadAllText(appScriptPath);
        AppBuildScript game = AppBuildScript.Load(appScriptPath);

        using (new FileStream(appScriptPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.That(() => game.AddDepotAndSave("100012"), Throws.InstanceOf<IOException>());
        }

        Assert.That(game.Depots, Has.Count.EqualTo(1));
        Assert.That(File.ReadAllText(appScriptPath), Is.EqualTo(appTextBefore));
        Assert.That(File.Exists(Path.Combine(folder.ScriptsPath, "depot_100012.vdf")), Is.False);
        Assert.That(Directory.GetFiles(folder.ScriptsPath), Has.Length.EqualTo(2));

        game.Description = "1.1";
        game.Save();

        Assert.That(AppBuildScript.Load(appScriptPath).Depots, Has.Count.EqualTo(1));
    }

    [Test]
    public void RemoveDepotAndSave_RemovesTheLineFromTheAppScriptAndKeepsTheDepotScript()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        AppBuildScript game = AppBuildScript.Load(WriteScriptsWithRelativePaths(folder));
        DepotBuildScript removedDepot = game.Depots[1];

        game.RemoveDepotAndSave(removedDepot);

        Assert.That(game.Depots.Select(depot => depot.DepotId), Is.EqualTo(new[] { "1", "3" }));
        AppBuildScript reread = AppBuildScript.Load(game.FilePath);
        Assert.That(reread.Depots.Select(depot => depot.DepotId), Is.EqualTo(new[] { "1", "3" }));
        Assert.That(reread.AbsoluteContentFolder(reread.Depots[1]), Is.EqualTo("E:\\builds\\linux"));
        Assert.That(File.Exists(removedDepot.FilePath), Is.True);
    }

    [Test]
    public void RemoveDepotAndSave_KeepsTheDepotWhenTheAppScriptCannotBeReplaced()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = WriteScriptsWithRelativePaths(folder);
        string appTextBefore = File.ReadAllText(appScriptPath);
        AppBuildScript game = AppBuildScript.Load(appScriptPath);

        using (new FileStream(appScriptPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.That(() => game.RemoveDepotAndSave(game.Depots[1]), Throws.InstanceOf<IOException>());
        }

        Assert.That(game.Depots.Select(depot => depot.DepotId), Is.EqualTo(new[] { "1", "2", "3" }));
        Assert.That(File.ReadAllText(appScriptPath), Is.EqualTo(appTextBefore));

        game.RemoveDepotAndSave(game.Depots[1]);

        Assert.That(AppBuildScript.Load(appScriptPath).Depots.Select(depot => depot.DepotId),
            Is.EqualTo(new[] { "1", "3" }));
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
    public void AbsoluteContentFolder_PutsAFolderThatStartsWithOneSeparatorOnTheDriveOfTheScript()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        AppBuildScript game = AppBuildScript.Load(WriteScriptsWithRelativePaths(folder));

        Assert.That(game.AbsoluteContentFolder(game.Depots[0], @"\builds\windows"),
            Is.EqualTo(Path.GetPathRoot(folder.ScriptsPath) + @"builds\windows"));
    }

    [Test]
    public void AbsoluteContentFolder_ResolvesParentFoldersWhenTheScriptPathHasTheExtendedLengthPrefix()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        Directory.CreateDirectory(Path.Combine(folder.ContentBuilderPath, "content"));
        AppBuildScript game = AppBuildScript.Load(@"\\?\" + WriteScriptsWithRelativePaths(folder));

        string absoluteFolder = game.AbsoluteContentFolder(game.Depots[0]);

        Assert.That(absoluteFolder, Is.EqualTo(@"\\?\" + Path.Combine(folder.ContentBuilderPath, "content")));
        Assert.That(ContentFolderSummary.Describe(absoluteFolder), Is.EqualTo("Empty folder"));
        Assert.That(game.Depots[1].FilePath, Is.EqualTo(@"\\?\" + Path.Combine(folder.ScriptsPath, "depot_2.vdf")));
    }

    [TestCase(@"\\?\C:\Depot\scripts\depot_1.vdf", @"..\content", @"\\?\C:\Depot\content")]
    [TestCase(@"\\?\C:\Depot\scripts\depot_1.vdf", @"..\..\..\content", @"\\?\C:\content")]
    [TestCase(@"\\?\C:\Depot\scripts\depot_1.vdf", @"..\.\content.", @"\\?\C:\Depot\content.")]
    [TestCase(@"\\?\C:\Depot\scripts\depot_1.vdf", "../content/windows", @"\\?\C:\Depot\content\windows")]
    [TestCase(@"\\?\C:\Depot\scripts\depot_1.vdf", "depot_2.vdf", @"\\?\C:\Depot\scripts\depot_2.vdf")]
    [TestCase(@"\\?\C:\Depot\scripts\depot_1.vdf", @"\\?\D:\builds\windows", @"\\?\D:\builds\windows")]
    [TestCase(@"\\?\C:\Depot\scripts\depot_1.vdf", @"D:\builds\..\windows", @"D:\windows")]
    [TestCase(@"\\?\UNC\server\share\Depot\scripts\depot_1.vdf", @"..\content", @"\\?\UNC\server\share\Depot\content")]
    [TestCase(@"\\?\UNC\server\share\Depot\scripts\depot_1.vdf", @"..\..\..\..\content",
        @"\\?\UNC\server\share\content")]
    [TestCase(@"\\?\C:\Depot\scripts\depot_1.vdf", @"C:..\content", @"\\?\C:\Depot\content")]
    [TestCase(@"\\?\C:\Depot\scripts\depot_1.vdf", @"D:builds\content.", @"\\?\D:\builds\content.")]
    [TestCase(@"\\?\UNC\server\share\Depot\scripts\depot_1.vdf", @"D:builds\windows", @"\\?\D:\builds\windows")]
    [TestCase(@"C:\Depot\scripts\depot_1.vdf", @"D:builds\windows", @"D:\builds\windows")]
    [TestCase(@"\\?\C:\Depot\scripts\depot_1.vdf", @"\content", @"\\?\C:\content")]
    [TestCase(@"C:\Depot\scripts\depot_1.vdf", @"C:..\content", @"C:\Depot\content")]
    [TestCase(@"C:\Depot\scripts\depot_1.vdf", @"..\content", @"C:\Depot\content")]
    public void AbsolutePath_ResolvesParentFoldersAndKeepsTheExtendedLengthPrefix(string scriptPath,
        string pathInScript, string expected)
    {
        BuildScriptFile script = BuildScriptFile.NotYetWritten(scriptPath, new KeyValuesNode("DepotBuildConfig"));

        Assert.That(script.AbsolutePath(pathInScript), Is.EqualTo(expected));
    }

    [Test]
    public void AbsolutePath_KeepsTheExtendedLengthPrefixOfAPathLongerThanMaxPath()
    {
        BuildScriptFile script = BuildScriptFile.NotYetWritten(@"\\?\C:\Depot\scripts\depot_1.vdf",
            new KeyValuesNode("DepotBuildConfig"));
        string longFolderName = new string('a', 300);

        Assert.That(script.AbsolutePath($@"..\{longFolderName}\windows"),
            Is.EqualTo($@"\\?\C:\Depot\{longFolderName}\windows"));
    }

    [TestCase("builds|windows")]
    [TestCase("E:\\builds\\<windows>")]
    [TestCase("E:\\builds:windows")]
    [TestCase("builds\\win*")]
    public void AbsoluteContentFolder_ReturnsAFolderThatIsNotAPathAsWritten(string contentFolder)
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        AppBuildScript game = AppBuildScript.Load(WriteScriptsWithRelativePaths(folder));

        string absoluteFolder = game.AbsoluteContentFolder(game.Depots[0], contentFolder);

        Assert.That(absoluteFolder, Is.EqualTo(contentFolder));
        Assert.That(ContentFolderSummary.Describe(absoluteFolder), Is.EqualTo("Folder not found"));
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
