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
    public void Describe_CountsAroundSubfoldersItCannotList()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string buildFolder = Path.Combine(folder.ContentBuilderPath, "build");
        Directory.CreateDirectory(Path.Combine(buildFolder, "data"));
        File.WriteAllText(Path.Combine(buildFolder, "data", "level.pak"), "xyz");
        using FolderThatDeniesListing archive = new FolderThatDeniesListing(Path.Combine(buildFolder, "Archive"));
        using FolderThatDeniesListing cache =
            new FolderThatDeniesListing(Path.Combine(buildFolder, "data", "Cache"));

        ContentFolderSummary summary = ContentFolderSummary.Describe(buildFolder);

        Assert.That(summary.FileCount, Is.EqualTo(1));
        Assert.That(summary.TotalBytes, Is.EqualTo(3));
        Assert.That(summary.UnreadableSubfolderCount, Is.EqualTo(2));
        Assert.That(summary.Text, Does.Match(@"^1 files, 0\.0 MB, newest [\d\- :]+, 2 subfolders not readable$"));
        Assert.That(ContentFolderSummary.FolderHasFiles(buildFolder), Is.True);
    }

    [Test]
    public void Describe_NamesAnEmptyFolderWhoseOnlySubfolderCannotBeListed()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string buildFolder = Path.Combine(folder.ContentBuilderPath, "build");
        using FolderThatDeniesListing archive = new FolderThatDeniesListing(Path.Combine(buildFolder, "Archive"));

        ContentFolderSummary summary = ContentFolderSummary.Describe(buildFolder);

        Assert.That(summary.Text, Is.EqualTo("Empty folder, 1 subfolder not readable"));
        Assert.That(summary.TextWithExactSizeAndTime,
            Does.Match("^Empty folder, 1 subfolder not readable, file list [0-9A-F]{8}$"));
        Assert.That(summary.HasFiles, Is.False);
        Assert.That(summary.UnreadableSubfolderCount, Is.EqualTo(1));
        Assert.That(ContentFolderSummary.FolderHasFiles(buildFolder), Is.False);
    }

    [Test]
    public void Describe_DiffersWhenTheOnlySubfolderItCannotListIsRenamed()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string buildFolder = Path.Combine(folder.ContentBuilderPath, "build");
        ContentFolderSummary confirmedContent;
        using (new FolderThatDeniesListing(Path.Combine(buildFolder, "Archive")))
        {
            confirmedContent = ContentFolderSummary.Describe(buildFolder);
        }

        Directory.Move(Path.Combine(buildFolder, "Archive"), Path.Combine(buildFolder, "Archive of another build"));
        using FolderThatDeniesListing renamed =
            new FolderThatDeniesListing(Path.Combine(buildFolder, "Archive of another build"));
        ContentFolderSummary contentNow = ContentFolderSummary.Describe(buildFolder);

        Assert.That(contentNow.Text, Is.EqualTo(confirmedContent.Text));
        Assert.That(contentNow, Is.Not.EqualTo(confirmedContent));
        Assert.That(contentNow.TextWithExactSizeAndTime, Is.Not.EqualTo(confirmedContent.TextWithExactSizeAndTime));
        Assert.That(ContentFolderSummary.Describe(Path.Combine(folder.ContentBuilderPath, "scripts"))
            .TextWithExactSizeAndTime, Is.EqualTo("Empty folder"));
    }

    [Test]
    public void Describe_FailsWhenTheFolderItselfCannotBeListed()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string buildFolder = Path.Combine(folder.ContentBuilderPath, "build");
        using FolderThatDeniesListing denied = new FolderThatDeniesListing(buildFolder);

        Assert.That(() => ContentFolderSummary.Describe(buildFolder), Throws.InstanceOf<UnauthorizedAccessException>());
        Assert.That(ContentFolderSummary.DescribeOrGiveReadError(buildFolder).HasFiles, Is.False);
        Assert.That(ContentFolderSummary.FolderHasFiles(buildFolder), Is.False);
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
