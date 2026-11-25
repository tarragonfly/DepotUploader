namespace DepotUploader.Tests;

public class ContentFolderSummaryTests
{
    [Test]
    public void Describe_HasNoFilesWhenNoFolderIsSet()
    {
        Assert.That(ContentFolderSummary.Describe(string.Empty),
            Is.EqualTo(new ContentFolderSummary("No folder set", HasFiles: false)));
    }

    [Test]
    public void Describe_CountsTheFilesInSubfolders()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string buildFolder = Path.Combine(folder.ContentBuilderPath, "build");
        Directory.CreateDirectory(Path.Combine(buildFolder, "data"));
        File.WriteAllText(Path.Combine(buildFolder, "game.exe"), "x");
        File.WriteAllText(Path.Combine(buildFolder, "data", "level.pak"), "x");

        ContentFolderSummary summary = ContentFolderSummary.Describe(buildFolder);

        Assert.That(summary.HasFiles, Is.True);
        Assert.That(summary.Text, Does.StartWith("2 files, 0.0 MB, newest "));
        Assert.That(summary.ToString(), Is.EqualTo(summary.Text));
    }

    [Test]
    public void FolderHasFiles_FindsAFileInASubfolder()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string buildFolder = Path.Combine(folder.ContentBuilderPath, "build");
        Directory.CreateDirectory(Path.Combine(buildFolder, "data"));

        Assert.That(ContentFolderSummary.FolderHasFiles(buildFolder), Is.False);

        File.WriteAllText(Path.Combine(buildFolder, "data", "level.pak"), "x");

        Assert.That(ContentFolderSummary.FolderHasFiles(buildFolder), Is.True);
    }

    [Test]
    public void FolderHasFiles_IsFalseWhenNoFolderIsSetOrTheFolderIsMissing()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();

        Assert.That(ContentFolderSummary.FolderHasFiles(string.Empty), Is.False);
        Assert.That(ContentFolderSummary.FolderHasFiles(Path.Combine(folder.ContentBuilderPath, "missing")), Is.False);
    }
}
