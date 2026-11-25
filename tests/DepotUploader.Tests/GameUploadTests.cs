namespace DepotUploader.Tests;

public class GameUploadTests
{
    [Test]
    public void FromScripts_KeepsTheValuesReadBeforeALaterEdit()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string appScriptPath = folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100030", "100031",
            folder.WriteContentFolderWithTwoFiles("100030"));
        AppBuildScript game = AppBuildScript.Load(appScriptPath);
        GameUpload upload =
            GameUpload.FromScripts(game, "Neon Garden", Path.Combine(folder.ContentBuilderPath, "copies"));

        game.Description = "typed after the click";
        game.LiveBranch = "default";
        game.UploadsNothing = true;
        game.Depots[0].ContentFolder = Path.Combine(folder.ContentBuilderPath, "another folder");

        Assert.That(upload.GameName, Is.EqualTo("Neon Garden"));
        Assert.That(upload.AppId, Is.EqualTo("100030"));
        Assert.That(upload.Description, Is.EqualTo("1.0 hotfix 1"));
        Assert.That(upload.LiveBranch, Is.Empty);
        Assert.That(upload.UploadsNothing, Is.False);
        Assert.That(upload.Depots, Is.EqualTo(new[] { ("100031", folder.ContentFolderPath("100030")) }));
        Assert.That(File.ReadAllText(upload.AppScriptCopyPath), Does.Contain("1.0 hotfix 1"));
        Assert.That(upload.BuildLogPath, Is.EqualTo(game.BuildLogPath));
    }

    [Test]
    public void DescribeContent_DiffersWhenAFileOfTheSameSizeIsWrittenOneSecondLater()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload upload = WriteGameAndReadUpload(folder, "100030", "Neon Garden", string.Empty, false);
        ContentFolderSummary[] confirmedContent = upload.DescribeContent(CancellationToken.None);
        string gamePath = Path.Combine(folder.ContentFolderPath("100030"), "game.exe");

        File.WriteAllText(gamePath, "y");
        File.SetLastWriteTimeUtc(gamePath, new DateTime(2026, 5, 1, 12, 0, 11, DateTimeKind.Utc));
        ContentFolderSummary[] contentNow = upload.DescribeContent(CancellationToken.None);

        Assert.That(contentNow[0].Text, Is.EqualTo(confirmedContent[0].Text));
        Assert.That(contentNow[0], Is.Not.EqualTo(confirmedContent[0]));
        Assert.That(contentNow[0].TextWithExactSizeAndTime, Is.Not.EqualTo(confirmedContent[0].TextWithExactSizeAndTime));
    }

    [Test]
    public void DescribeContent_IsEqualWhenNoFileChanged()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload upload = WriteGameAndReadUpload(folder, "100030", "Neon Garden", string.Empty, false);

        Assert.That(upload.DescribeContent(CancellationToken.None),
            Is.EqualTo(upload.DescribeContent(CancellationToken.None)));
    }

    [Test]
    public void DescribeContent_DiffersWhenAFileIsAdded()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload upload = WriteGameAndReadUpload(folder, "100030", "Neon Garden", string.Empty, false);
        ContentFolderSummary confirmedContent = upload.DescribeContent(CancellationToken.None)[0];
        string addedFilePath = Path.Combine(folder.ContentFolderPath("100030"), "added.bin");

        File.WriteAllText(addedFilePath, "x");
        File.SetLastWriteTimeUtc(addedFilePath, new DateTime(2026, 5, 1, 12, 0, 10, DateTimeKind.Utc));

        Assert.That(upload.DescribeContent(CancellationToken.None)[0], Is.Not.EqualTo(confirmedContent));
    }

    [Test]
    public void DescribeContent_RefusesAContentFolderThatIsGone()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload upload = WriteGameAndReadUpload(folder, "100030", "Neon Garden", string.Empty, false);
        Directory.Delete(folder.ContentFolderPath("100030"), true);

        Assert.That(() => upload.DescribeContent(CancellationToken.None), Throws.InstanceOf<IOException>()
            .With.Message.EqualTo("Depot 100031 of Neon Garden (100030) has no content folder at "
                + $"\"{folder.ContentFolderPath("100030")}\"."));
    }

    [Test]
    public void DescribeContent_CountsAroundASubfolderItCannotListAndStillSeesAChangedFile()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload upload = WriteGameAndReadUpload(folder, "100030", "Neon Garden", string.Empty, false);
        ContentFolderSummary contentWithoutTheSubfolder = upload.DescribeContent(CancellationToken.None)[0];
        using FolderThatDeniesListing archive =
            new FolderThatDeniesListing(Path.Combine(folder.ContentFolderPath("100030"), "Archive"));

        ContentFolderSummary confirmedContent = upload.DescribeContent(CancellationToken.None)[0];
        File.WriteAllText(Path.Combine(folder.ContentFolderPath("100030"), "game.exe"), "a larger build");
        ContentFolderSummary contentNow = upload.DescribeContent(CancellationToken.None)[0];

        Assert.That(confirmedContent.FileCount, Is.EqualTo(2));
        Assert.That(confirmedContent.UnreadableSubfolderCount, Is.EqualTo(1));
        Assert.That(confirmedContent.Text, Does.EndWith(", 1 subfolder not readable"));
        Assert.That(confirmedContent.TextWithExactSizeAndTime, Does.Contain(", 1 subfolder not readable, file list "));
        Assert.That(confirmedContent, Is.Not.EqualTo(contentWithoutTheSubfolder));
        Assert.That(contentNow, Is.Not.EqualTo(confirmedContent));
    }

    [Test]
    public void DescribeContent_RefusesAContentFolderItCannotList()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload upload = WriteGameAndReadUpload(folder, "100030", "Neon Garden", string.Empty, false);
        using FolderThatDeniesListing contentFolder = new FolderThatDeniesListing(folder.ContentFolderPath("100030"));

        Assert.That(() => upload.DescribeContent(CancellationToken.None), Throws.InstanceOf<IOException>()
            .With.Message.StartsWith("The files of depot 100031 of Neon Garden (100030) in "
                + $"\"{folder.ContentFolderPath("100030")}\" could not be counted. "));
    }

    [Test]
    public void DescribeContent_CountsAnEmptyContentFolder()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload upload = WriteGameAndReadUpload(folder, "100030", "Neon Garden", string.Empty, false);
        foreach (string filePath in Directory.GetFiles(folder.ContentFolderPath("100030")))
        {
            File.Delete(filePath);
        }

        Assert.That(upload.DescribeContent(CancellationToken.None)[0],
            Is.EqualTo(new ContentFolderSummary("Empty folder", HasFiles: false)));
    }

    [Test]
    public void FromScripts_RefusesADepotWithoutAContentFolderAndNamesTheGame()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        AppBuildScript game = AppBuildScript.Load(
            folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100030", "100031", string.Empty));
        string folderForScriptCopies = Path.Combine(folder.ContentBuilderPath, "copies");

        Assert.That(() => GameUpload.FromScripts(game, "Neon Garden", folderForScriptCopies),
            Throws.InvalidOperationException.With.Message.EqualTo(
                "Depot 100031 of Neon Garden (100030) has no content folder."));
        Assert.That(Directory.Exists(folderForScriptCopies), Is.False);
    }

    [Test]
    public void DescribeContentUnlessStoppedAsync_PassesOnThatAContentFolderIsGone()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload upload = WriteGameAndReadUpload(folder, "100030", "Neon Garden", string.Empty, false);
        Directory.Delete(folder.ContentFolderPath("100030"), true);

        Assert.That(async () => await GameUpload.DescribeContentUnlessStoppedAsync(new[] { upload },
            CancellationToken.None), Throws.InstanceOf<IOException>());
    }

    [Test]
    public void DescribeContent_StopsWhenCancellationIsRequested()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload upload = WriteGameAndReadUpload(folder, "100030", "Neon Garden", string.Empty, false);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.That(() => upload.DescribeContent(cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task DescribeContentUnlessStoppedAsync_DescribesTheContentOfEachGame()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload[] uploads =
        {
            WriteGameAndReadUpload(folder, "100030", "Neon Garden", string.Empty, false),
            WriteGameAndReadUpload(folder, "100040", "Paper Kingdom", string.Empty, false),
        };

        ContentFolderSummary[][]? content =
            await GameUpload.DescribeContentUnlessStoppedAsync(uploads, CancellationToken.None);

        Assert.That(content, Has.Length.EqualTo(2));
        Assert.That(content![0], Is.EqualTo(uploads[0].DescribeContent(CancellationToken.None)));
        Assert.That(content[1][0].FileCount, Is.EqualTo(2));
    }

    [Test]
    public async Task DescribeContentUnlessStoppedAsync_ReturnsNothingAfterAStop()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload upload = WriteGameAndReadUpload(folder, "100030", "Neon Garden", string.Empty, false);
        using CancellationTokenSource stop = new CancellationTokenSource();
        stop.Cancel();

        Assert.That(await GameUpload.DescribeContentUnlessStoppedAsync(new[] { upload }, stop.Token), Is.Null);
    }

    [Test]
    public void DescribeContent_DiffersWhenAFileIsRenamedOrMovedWithTheSameSizeAndWriteTime()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload upload = WriteGameAndReadUpload(folder, "100030", "Neon Garden", string.Empty, false);
        string contentFolder = folder.ContentFolderPath("100030");
        ContentFolderSummary confirmedContent = upload.DescribeContent(CancellationToken.None)[0];

        File.Move(Path.Combine(contentFolder, "game.exe"), Path.Combine(contentFolder, "game-of-another-build.exe"));
        ContentFolderSummary contentWithRenamedFile = upload.DescribeContent(CancellationToken.None)[0];
        File.Move(Path.Combine(contentFolder, "game-of-another-build.exe"), Path.Combine(contentFolder, "game.exe"));
        ContentFolderSummary contentWithTheNameBack = upload.DescribeContent(CancellationToken.None)[0];
        Directory.CreateDirectory(Path.Combine(contentFolder, "bin"));
        File.Move(Path.Combine(contentFolder, "game.exe"), Path.Combine(contentFolder, "bin", "game.exe"));
        ContentFolderSummary contentWithMovedFile = upload.DescribeContent(CancellationToken.None)[0];

        Assert.That(contentWithRenamedFile.Text, Is.EqualTo(confirmedContent.Text));
        Assert.That(contentWithRenamedFile.TotalBytes, Is.EqualTo(confirmedContent.TotalBytes));
        Assert.That(contentWithRenamedFile.NewestWriteUtc, Is.EqualTo(confirmedContent.NewestWriteUtc));
        Assert.That(contentWithRenamedFile, Is.Not.EqualTo(confirmedContent));
        Assert.That(contentWithRenamedFile.TextWithExactSizeAndTime,
            Is.Not.EqualTo(confirmedContent.TextWithExactSizeAndTime));
        Assert.That(contentWithTheNameBack, Is.EqualTo(confirmedContent));
        Assert.That(contentWithMovedFile, Is.Not.EqualTo(confirmedContent));
    }

    [Test]
    public void DescribeContent_DiffersWhenTwoFilesSwapTheirSizesAndTheTotalStaysTheSame()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload upload = WriteGameAndReadUpload(folder, "100030", "Neon Garden", string.Empty, false);
        string contentFolder = folder.ContentFolderPath("100030");
        DateTime writeTime = new DateTime(2026, 5, 1, 12, 0, 10, DateTimeKind.Utc);
        File.WriteAllText(Path.Combine(contentFolder, "game.exe"), "xx");
        File.WriteAllText(Path.Combine(contentFolder, "data.pak"), "xxxx");
        File.SetLastWriteTimeUtc(Path.Combine(contentFolder, "game.exe"), writeTime);
        File.SetLastWriteTimeUtc(Path.Combine(contentFolder, "data.pak"), writeTime);
        ContentFolderSummary confirmedContent = upload.DescribeContent(CancellationToken.None)[0];

        File.WriteAllText(Path.Combine(contentFolder, "game.exe"), "xxxx");
        File.WriteAllText(Path.Combine(contentFolder, "data.pak"), "xx");
        File.SetLastWriteTimeUtc(Path.Combine(contentFolder, "game.exe"), writeTime);
        File.SetLastWriteTimeUtc(Path.Combine(contentFolder, "data.pak"), writeTime);
        ContentFolderSummary contentNow = upload.DescribeContent(CancellationToken.None)[0];

        Assert.That(contentNow.FileCount, Is.EqualTo(confirmedContent.FileCount));
        Assert.That(contentNow.TotalBytes, Is.EqualTo(confirmedContent.TotalBytes));
        Assert.That(contentNow.NewestWriteUtc, Is.EqualTo(confirmedContent.NewestWriteUtc));
        Assert.That(contentNow, Is.Not.EqualTo(confirmedContent));
    }

    [Test]
    public void WhyTwoUploadsWouldSetTheSameBranchLive_NamesTwoScriptsOfOneAppWithTheSameBranch()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload first = WriteGameAndReadUpload(folder, "100030", "Neon Garden", "beta", false);
        GameUpload copy = first with { AppId = "0100030", AppScriptFileName = "app_100031.vdf", LiveBranch = "Beta" };
        GameUpload otherGame = WriteGameAndReadUpload(folder, "100040", "Paper Kingdom", "beta", false);

        Assert.That(GameUpload.WhyTwoUploadsWouldSetTheSameBranchLive(new[] { otherGame, first, copy }),
            Is.EqualTo("app_100030.vdf and app_100031.vdf both upload app 100030 and set it live on branch beta. "
                + "The second build would replace the first on that branch. Untick one of the two."));
    }

    [TestCase(" 100030")]
    [TestCase("+100030")]
    [TestCase("100030 copy")]
    [TestCase("1.0003E+05")]
    [TestCase("-100030")]
    [TestCase("")]
    public void FromScripts_RefusesAnAppIdThatIsNotDigitsOnly(string appId)
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        AppBuildScript game = AppBuildScript.Load(folder.WriteAppAndDepotScriptsLikeSteamPipeGui(appId, "100031",
            folder.WriteContentFolderWithTwoFiles("100030")));
        string folderForScriptCopies = Path.Combine(folder.ContentBuilderPath, "copies");

        Assert.That(() => GameUpload.FromScripts(game, "Neon Garden", folderForScriptCopies),
            Throws.InvalidOperationException.With.Message.EqualTo(
                $"The app ID \"{appId}\" in app_{appId}.vdf is not a number."));
        Assert.That(Directory.Exists(folderForScriptCopies), Is.False);
    }

    [TestCase("0100030", " beta ")]
    [TestCase("000100030", "BETA")]
    [TestCase("100030", "beta")]
    public void WhyTwoUploadsWouldSetTheSameBranchLive_IgnoresLeadingZerosOfTheAppIdAndSpacesAroundTheBranch(
        string appIdOfTheCopy, string liveBranchOfTheCopy)
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload first = WriteGameAndReadUpload(folder, "100030", "Neon Garden", "beta", false);
        GameUpload copy = first with
        {
            AppId = appIdOfTheCopy,
            AppScriptFileName = "app_100031.vdf",
            LiveBranch = liveBranchOfTheCopy,
        };

        Assert.That(GameUpload.WhyTwoUploadsWouldSetTheSameBranchLive(new[] { first, copy }),
            Does.StartWith("app_100030.vdf and app_100031.vdf both upload app 100030 and set it live on branch beta."));
    }

    [TestCase("1000300", "beta")]
    [TestCase("10003", "beta")]
    [TestCase("100030", "beta2")]
    [TestCase("100030", " ")]
    public void WhyTwoUploadsWouldSetTheSameBranchLive_AllowsAnotherNumberOrAnotherBranch(string appIdOfTheOther,
        string liveBranchOfTheOther)
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload first = WriteGameAndReadUpload(folder, "100030", "Neon Garden", "beta", false);
        GameUpload other = first with { AppId = appIdOfTheOther, LiveBranch = liveBranchOfTheOther };

        Assert.That(GameUpload.WhyTwoUploadsWouldSetTheSameBranchLive(new[] { first, other }), Is.Null);
    }

    [Test]
    public void WhyTwoUploadsWouldSetTheSameBranchLive_AllowsOtherBranchesNoBranchAndPreviewBuilds()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload first = WriteGameAndReadUpload(folder, "100030", "Neon Garden", "beta", false);
        GameUpload otherGame = WriteGameAndReadUpload(folder, "100040", "Paper Kingdom", "beta", false);

        Assert.That(GameUpload.WhyTwoUploadsWouldSetTheSameBranchLive(new[]
        {
            first,
            otherGame,
            first with { LiveBranch = "default" },
            first with { UploadsNothing = true },
            first with { LiveBranch = string.Empty },
            first with { LiveBranch = string.Empty },
        }), Is.Null);
    }

    internal static GameUpload WriteGameAndReadUpload(TemporaryContentBuilderFolder folder, string appId,
        string gameName, string liveBranch, bool uploadsNothing)
    {
        string appScriptPath = folder.WriteAppAndDepotScriptsLikeSteamPipeGui(appId, appId.Substring(0, 5) + "1",
            folder.WriteContentFolderWithTwoFiles(appId));
        AppBuildScript game = AppBuildScript.Load(appScriptPath);
        game.LiveBranch = liveBranch;
        game.UploadsNothing = uploadsNothing;

        return GameUpload.FromScripts(game, gameName, Path.Combine(folder.ContentBuilderPath, "copies", appId));
    }
}
