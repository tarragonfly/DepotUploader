namespace DepotUploader.Tests;

public class ContentBuilderFolderTests
{
    [Test]
    public void LoadAppBuildScripts_ListsOnlyScriptsNamedAfterAnAppId()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\content\\FirstGame");
        folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100020", "100021", "E:\\content\\SecondGame_Windows");
        folder.WriteScript("app_build_100040.vdf",
            "\"appbuild\" { \"appid\" \"100040\" \"depots\" { \"100041\" \"missing.vdf\" } }");

        List<AppBuildScript> games = new ContentBuilderFolder(folder.ContentBuilderPath)
            .LoadAppBuildScripts(out List<string> scriptLoadErrors);

        Assert.That(scriptLoadErrors, Is.Empty);
        Assert.That(games.Select(game => game.AppId), Is.EqualTo(new[] { "100010", "100020" }));
        Assert.That(games[0].Description, Is.EqualTo("1.0 hotfix 1"));
        Assert.That(games[0].Depots[0].DepotId, Is.EqualTo("100011"));
        Assert.That(games[0].Depots[0].ContentFolder, Is.EqualTo("E:\\content\\FirstGame"));
    }

    [Test]
    public void LoadAppBuildScripts_LoadsTheOtherGamesWhenOneScriptCannotBeRead()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\content\\FirstGame");
        folder.WriteScript("app_999.vdf", "\"appbuild\" { \"appid\" \"999\" \"depots\" { \"1000\" \"missing.vdf\" } }");

        List<AppBuildScript> games = new ContentBuilderFolder(folder.ContentBuilderPath)
            .LoadAppBuildScripts(out List<string> scriptLoadErrors);

        Assert.That(games.Select(game => game.AppId), Is.EqualTo(new[] { "100010" }));
        Assert.That(scriptLoadErrors, Has.Exactly(1).StartsWith("app_999.vdf: "));
    }

    [Test]
    public void LoadAppBuildScripts_LoadsTheOtherGamesWhenADepotScriptPathIsNotAPath()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\content\\FirstGame");
        folder.WriteScript("app_999.vdf", "\"appbuild\" { \"appid\" \"999\" \"depots\" { \"1000\" \"depot|1000.vdf\" } }");

        List<AppBuildScript> games = new ContentBuilderFolder(folder.ContentBuilderPath)
            .LoadAppBuildScripts(out List<string> scriptLoadErrors);

        Assert.That(games.Select(game => game.AppId), Is.EqualTo(new[] { "100010" }));
        Assert.That(scriptLoadErrors, Has.Exactly(1).StartsWith("app_999.vdf: "));
    }

    [Test]
    public void Save_WritesTheEditedValuesAndKeepsTheOtherKeys()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\old");
        AppBuildScript game = AppBuildScript.Load(appScriptPath);

        game.Description = "1.1";
        game.LiveBranch = "beta";
        game.UploadsNothing = true;
        game.Depots[0].ContentFolder = "D:\\Builds\\FirstGame_1_1";
        game.Save();

        AppBuildScript reread = AppBuildScript.Load(appScriptPath);
        Assert.That(reread.Description, Is.EqualTo("1.1"));
        Assert.That(reread.LiveBranch, Is.EqualTo("beta"));
        Assert.That(reread.UploadsNothing, Is.True);
        Assert.That(reread.Depots[0].ContentFolder, Is.EqualTo("D:\\Builds\\FirstGame_1_1"));
        KeyValuesNode depotRoot = KeyValuesText.Parse(File.ReadAllText(reread.Depots[0].FilePath));
        Assert.That(depotRoot.GetValue("FileExclusion"), Is.EqualTo("*.pdb"));
        Assert.That(depotRoot.FindChild("FileMapping")!.GetValue("LocalPath"), Is.EqualTo("*"));
        Assert.That(KeyValuesText.Parse(File.ReadAllText(appScriptPath)).GetValue("buildoutput"),
            Is.EqualTo(Path.Combine(folder.ContentBuilderPath, "output")));
    }

    [Test]
    public void Save_WritesNoFileWhenAValueCannotBeWritten()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\old");
        string appTextBefore = File.ReadAllText(appScriptPath);
        AppBuildScript game = AppBuildScript.Load(appScriptPath);

        game.Description = "1.1";
        game.Depots[0].ContentFolder = "E:\\bad\"folder";

        Assert.That(() => game.Save(), Throws.TypeOf<FormatException>());
        Assert.That(File.ReadAllText(appScriptPath), Is.EqualTo(appTextBefore));
    }

    [Test]
    public void CreateAppBuildScript_WritesScriptsThatLoadAgain()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        ContentBuilderFolder contentBuilder = new ContentBuilderFolder(folder.ContentBuilderPath);

        contentBuilder.CreateAppBuildScript("100030", new[] { "100031", "100032" });

        AppBuildScript game = contentBuilder.LoadAppBuildScripts(out _).Single();
        Assert.That(game.AppId, Is.EqualTo("100030"));
        Assert.That(game.UploadsNothing, Is.False);
        Assert.That(game.Depots.Select(depot => depot.DepotId), Is.EqualTo(new[] { "100031", "100032" }));
        Assert.That(game.Depots.Select(depot => depot.ContentFolder), Is.All.Empty);
    }

    [Test]
    public void CreateAppBuildScript_RefusesAnAppIdThatAlreadyHasAScript()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\content");
        ContentBuilderFolder contentBuilder = new ContentBuilderFolder(folder.ContentBuilderPath);

        Assert.That(() => contentBuilder.CreateAppBuildScript("100010", new[] { "100012" }),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(File.Exists(Path.Combine(folder.ScriptsPath, "depot_100012.vdf")), Is.False);
    }

    [Test]
    public void CreateAppBuildScript_LeavesNoDepotScriptWhenTheAppScriptCannotBeWritten()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        Directory.CreateDirectory(Path.Combine(folder.ScriptsPath, "app_100030.vdf"));
        ContentBuilderFolder contentBuilder = new ContentBuilderFolder(folder.ContentBuilderPath);

        Assert.That(() => contentBuilder.CreateAppBuildScript("100030", new[] { "100031", "100032" }),
            Throws.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>());

        Assert.That(Directory.GetFiles(folder.ScriptsPath), Is.Empty);
    }

    [Test]
    public void MoveToRemovedGameScriptsFolder_TakesTheGameOffTheListAndMovingTheFilesBackListsItAgain()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\content\\FirstGame");
        folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100020", "100021", "E:\\content\\SecondGame");
        ContentBuilderFolder contentBuilder = new ContentBuilderFolder(folder.ContentBuilderPath);
        List<AppBuildScript> games = contentBuilder.LoadAppBuildScripts(out _);

        List<string> scriptPaths = contentBuilder.AppScriptAndDepotScriptsOnlyItUses(games[0].FilePath,
            games[0].AppId);
        contentBuilder.MoveToRemovedGameScriptsFolder(scriptPaths);

        Assert.That(scriptPaths.Select(Path.GetFileName), Is.EqualTo(new[] { "app_100010.vdf", "depot_100011.vdf" }));
        Assert.That(contentBuilder.LoadAppBuildScripts(out List<string> scriptLoadErrors).Select(game => game.AppId),
            Is.EqualTo(new[] { "100020" }));
        Assert.That(scriptLoadErrors, Is.Empty);

        foreach (string scriptPath in scriptPaths)
        {
            File.Move(Path.Combine(contentBuilder.RemovedGameScriptsFolder, Path.GetFileName(scriptPath)), scriptPath);
        }

        Assert.That(contentBuilder.LoadAppBuildScripts(out _).Select(game => game.AppId),
            Is.EqualTo(new[] { "100010", "100020" }));
    }

    [Test]
    public void AppScriptAndDepotScriptsOnlyItUses_LeavesOutADepotScriptOutsideTheScriptsFolder()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        File.WriteAllText(Path.Combine(folder.ContentBuilderPath, "depot_100012.vdf"),
            "\"DepotBuildConfig\" { \"DepotID\" \"100012\" }");
        string appScriptPath = folder.WriteScript("app_100010.vdf",
            "\"appbuild\" { \"appid\" \"100010\" \"depots\" { \"100012\" \"..\\depot_100012.vdf\" } }");

        Assert.That(new ContentBuilderFolder(folder.ContentBuilderPath)
                .AppScriptAndDepotScriptsOnlyItUses(appScriptPath, "100010"),
            Is.EqualTo(new[] { appScriptPath }));
    }

    [Test]
    public void AppScriptAndDepotScriptsOnlyItUses_LeavesOutADepotScriptThatAnAppScriptWithAMissingDepotUses()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011",
            "E:\\content\\FirstGame");
        folder.WriteScript("app_build_100020.vdf", "\"appbuild\" { \"appid\" \"100020\" \"depots\" { "
            + "\"100011\" \"DEPOT_100011.vdf\" \"100021\" \"missing.vdf\" } }");

        Assert.That(new ContentBuilderFolder(folder.ContentBuilderPath)
                .AppScriptAndDepotScriptsOnlyItUses(appScriptPath, "100010"),
            Is.EqualTo(new[] { appScriptPath }));
    }

    [Test]
    public void AppScriptAndDepotScriptsOnlyItUses_LeavesOutAnotherAppScriptWrittenAsADepotScript()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100020", "100021", "E:\\content\\SecondGame");
        string appScriptPath = folder.WriteScript("app_100010.vdf",
            "\"appbuild\" { \"appid\" \"100010\" \"depots\" { \"100011\" \"app_100020.vdf\" } }");

        Assert.That(new ContentBuilderFolder(folder.ContentBuilderPath)
                .AppScriptAndDepotScriptsOnlyItUses(appScriptPath, "100010"),
            Is.EqualTo(new[] { appScriptPath }));
    }

    [Test]
    public void AppScriptAndDepotScriptsOnlyItUses_LeavesOutEveryDepotScriptWhenAnotherScriptCannotBeRead()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011",
            "E:\\content\\FirstGame");
        folder.WriteScript("app_100020.vdf", "\"appbuild\" { \"appid\" \"100020\" \"depots\" {");

        Assert.That(new ContentBuilderFolder(folder.ContentBuilderPath)
                .AppScriptAndDepotScriptsOnlyItUses(appScriptPath, "100010"),
            Is.EqualTo(new[] { appScriptPath }));
    }

    [Test]
    public void AppScriptAndDepotScriptsOnlyItUses_RefusesAnAppScriptWhoseAppIdChangedOnDisk()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100020", "100021", "E:\\content\\SecondGame");
        string appScriptPath = folder.WriteScript("app_100010.vdf",
            "\"appbuild\" { \"appid\" \"100020\" \"depots\" { \"100021\" \"depot_100021.vdf\" } }");

        Assert.That(() => new ContentBuilderFolder(folder.ContentBuilderPath)
                .AppScriptAndDepotScriptsOnlyItUses(appScriptPath, "100010"),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void AppScriptAndDepotScriptsOnlyItUses_RefusesAFileThatAnotherScriptUsesAsADepotScript()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string depotScriptNamedLikeAnAppScript = folder.WriteScript("app_100011.vdf",
            "\"DepotBuildConfig\" { \"DepotID\" \"100011\" }");
        folder.WriteScript("app_100010.vdf",
            "\"appbuild\" { \"appid\" \"100010\" \"depots\" { \"100011\" \"app_100011.vdf\" } }");

        Assert.That(() => new ContentBuilderFolder(folder.ContentBuilderPath)
                .AppScriptAndDepotScriptsOnlyItUses(depotScriptNamedLikeAnAppScript, string.Empty),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void MoveToRemovedGameScriptsFolder_MovesNothingWhenTheFolderAlreadyHasAScriptOfThatName()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\content\\FirstGame");
        ContentBuilderFolder contentBuilder = new ContentBuilderFolder(folder.ContentBuilderPath);
        AppBuildScript game = contentBuilder.LoadAppBuildScripts(out _).Single();
        Directory.CreateDirectory(contentBuilder.RemovedGameScriptsFolder);
        File.WriteAllText(Path.Combine(contentBuilder.RemovedGameScriptsFolder, "depot_100011.vdf"), "earlier removal");

        Assert.That(() => contentBuilder.MoveToRemovedGameScriptsFolder(
                contentBuilder.AppScriptAndDepotScriptsOnlyItUses(game.FilePath, game.AppId)),
            Throws.TypeOf<InvalidOperationException>());

        Assert.That(contentBuilder.LoadAppBuildScripts(out _).Select(listedGame => listedGame.AppId),
            Is.EqualTo(new[] { "100010" }));
        Assert.That(Directory.GetFiles(contentBuilder.RemovedGameScriptsFolder).Select(Path.GetFileName),
            Is.EqualTo(new[] { "depot_100011.vdf" }));
    }

    [Test]
    public void MoveToRemovedGameScriptsFolder_MovesTheAppScriptBackWhenADepotScriptCannotBeMoved()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\content\\FirstGame");
        ContentBuilderFolder contentBuilder = new ContentBuilderFolder(folder.ContentBuilderPath);
        AppBuildScript game = contentBuilder.LoadAppBuildScripts(out _).Single();
        List<string> scriptPaths = contentBuilder.AppScriptAndDepotScriptsOnlyItUses(game.FilePath, game.AppId);

        using (new FileStream(game.Depots[0].FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.That(() => contentBuilder.MoveToRemovedGameScriptsFolder(scriptPaths),
                Throws.InstanceOf<IOException>());
        }

        Assert.That(contentBuilder.LoadAppBuildScripts(out _).Select(listedGame => listedGame.AppId),
            Is.EqualTo(new[] { "100010" }));
        Assert.That(Directory.GetFiles(contentBuilder.RemovedGameScriptsFolder), Is.Empty);
    }
}
