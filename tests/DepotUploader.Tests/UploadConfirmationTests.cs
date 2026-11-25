namespace DepotUploader.Tests;

public class UploadConfirmationTests
{
    [Test]
    public void Details_ListTheDescriptionBranchAndEachDepotOfOneGame()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload upload = GameUploadTests.WriteGameAndReadUpload(folder, "100030", "Neon Garden", "beta", false);
        ContentFolderSummary[] content = upload.DescribeContent(CancellationToken.None);

        Assert.That(UploadConfirmation.Question(new[] { upload }), Is.EqualTo("Upload Neon Garden (100030) to Steam?"));
        Assert.That(LinesOf(UploadConfirmation.Details(new[] { upload }, new[] { content })), Is.EqualTo(new[]
        {
            "Description: 1.0 hotfix 1",
            "Set live on branch: beta",
            $"Depot 100031: {folder.ContentFolderPath("100030")}",
            $"    {content[0].Text}",
        }));
        Assert.That(content[0].Text, Does.StartWith("2 files, 0.0 MB, newest "));
    }

    [Test]
    public void Question_AsksAboutAPreviewBuildOfOneGame()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload upload =
            GameUploadTests.WriteGameAndReadUpload(folder, "100030", "Neon Garden", string.Empty, true);

        Assert.That(UploadConfirmation.Question(new[] { upload }),
            Is.EqualTo("Run a preview build of Neon Garden (100030)? Nothing is uploaded."));
        Assert.That(LinesOf(UploadConfirmation.Details(new[] { upload }, ContentOf(new[] { upload })))[1],
            Is.EqualTo("Set live: no"));
    }

    [Test]
    public void Details_ListEveryGameOfABatchWithItsDepots()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload[] uploads =
        {
            GameUploadTests.WriteGameAndReadUpload(folder, "100030", "Neon Garden", "beta", false),
            GameUploadTests.WriteGameAndReadUpload(folder, "100040", "Paper Kingdom", string.Empty, true),
            GameUploadTests.WriteGameAndReadUpload(folder, "100050", "Zinc Lantern", string.Empty, false),
        };
        ContentFolderSummary[][] content = ContentOf(uploads);

        Assert.That(UploadConfirmation.Question(uploads),
            Is.EqualTo("Upload 2 games to Steam and run a preview build of 1 game?"));
        Assert.That(LinesOf(UploadConfirmation.Details(uploads, content)), Is.EqualTo(new[]
        {
            "Neon Garden (100030): \"1.0 hotfix 1\", set live on beta",
            $"    Depot 100031: {folder.ContentFolderPath("100030")}, {content[0][0].Text}",
            "Paper Kingdom (100040): preview build, nothing is uploaded",
            $"    Depot 100041: {folder.ContentFolderPath("100040")}, {content[1][0].Text}",
            "Zinc Lantern (100050): \"1.0 hotfix 1\", not set live",
            $"    Depot 100051: {folder.ContentFolderPath("100050")}, {content[2][0].Text}",
        }));
    }

    [Test]
    public void Question_AsksAboutUploadsOnlyOrPreviewBuildsOnlyOfABatch()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        GameUpload[] uploads =
        {
            GameUploadTests.WriteGameAndReadUpload(folder, "100030", "Neon Garden", string.Empty, false),
            GameUploadTests.WriteGameAndReadUpload(folder, "100040", "Paper Kingdom", string.Empty, false),
        };
        GameUpload[] previews =
        {
            GameUploadTests.WriteGameAndReadUpload(folder, "100050", "Zinc Lantern", string.Empty, true),
            GameUploadTests.WriteGameAndReadUpload(folder, "100060", "Amber Circuit", string.Empty, true),
        };

        Assert.That(UploadConfirmation.Question(uploads), Is.EqualTo("Upload 2 games to Steam?"));
        Assert.That(UploadConfirmation.Question(previews),
            Is.EqualTo("Run a preview build of 2 games? Nothing is uploaded."));
    }

    private static ContentFolderSummary[][] ContentOf(GameUpload[] uploads)
    {
        return uploads.Select(upload => upload.DescribeContent(CancellationToken.None)).ToArray();
    }

    private static string[] LinesOf(string text)
    {
        return text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
    }
}
