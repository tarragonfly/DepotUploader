using System.Text;

namespace DepotUploader.Tests;

public class AllOrNothingFileWriteTests
{
    private const string MovedAsideErrorText = "Unable to move the replacement file to the file to be replaced.";

    [Test]
    public void WriteAll_LeavesOnlyTheTargetsWithTheNewText()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string replacedPath = Path.Combine(folder.ScriptsPath, "app_100030.vdf");
        string newPath = Path.Combine(folder.ScriptsPath, "depot_100031.vdf");
        File.WriteAllText(replacedPath, "old app text");

        AllOrNothingFileWrite.WriteAll(new[] { (replacedPath, "new app text"), (newPath, "new depot text") });

        Assert.That(File.ReadAllBytes(replacedPath), Is.EqualTo(Encoding.UTF8.GetBytes("new app text")));
        Assert.That(File.ReadAllBytes(newPath), Is.EqualTo(Encoding.UTF8.GetBytes("new depot text")));
        Assert.That(Directory.GetFiles(folder.ScriptsPath).OrderBy(path => path), Is.EqualTo(new[] { replacedPath, newPath }));
    }

    [Test]
    public void WriteAll_KeepsTheAccessRulesAndAlternateStreamOfTheReplacedFile()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string filePath = Path.Combine(folder.ScriptsPath, "app_100030.vdf");
        string fileWithFolderRulesPath = Path.Combine(folder.ScriptsPath, "depot_100031.vdf");
        File.WriteAllText(filePath, "old text");
        File.WriteAllText(fileWithFolderRulesPath, "old text");
        TestFileMetadata.WriteAlternateStream(filePath);
        string accessRulesBefore = TestFileMetadata.GiveOwnAccessRules(filePath);

        AllOrNothingFileWrite.WriteAll(new[] { (filePath, "new text") });

        Assert.That(File.ReadAllText(filePath), Is.EqualTo("new text"));
        Assert.That(TestFileMetadata.AccessRulesOf(filePath), Is.EqualTo(accessRulesBefore));
        Assert.That(accessRulesBefore, Is.Not.EqualTo(TestFileMetadata.AccessRulesOf(fileWithFolderRulesPath)));
        Assert.That(TestFileMetadata.ReadAlternateStream(filePath), Is.EqualTo(TestFileMetadata.AlternateStreamBytes));
        Assert.That(Directory.GetFiles(folder.ScriptsPath).OrderBy(path => path),
            Is.EqualTo(new[] { filePath, fileWithFolderRulesPath }));
    }

    [Test]
    public void WriteAll_KeepsTheEncryptionOfTheReplacedFile()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string filePath = Path.Combine(folder.ScriptsPath, "app_100030.vdf");
        File.WriteAllText(filePath, "old text");
        if (!TestFileMetadata.TryEncrypt(filePath))
        {
            Assert.Ignore("EFS is not available on this Windows edition or volume.");
        }

        AllOrNothingFileWrite.WriteAll(new[] { (filePath, "new text") });

        Assert.That(File.ReadAllText(filePath), Is.EqualTo("new text"));
        Assert.That(File.GetAttributes(filePath).HasFlag(FileAttributes.Encrypted), Is.True);
        Assert.That(Directory.GetFiles(folder.ScriptsPath), Is.EqualTo(new[] { filePath }));
    }

    [Test]
    public void WriteAll_ChangesNothingWhenAFileCannotBeStaged()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string scriptPath = Path.Combine(folder.ScriptsPath, "app_100030.vdf");
        File.WriteAllText(scriptPath, "old text");
        File.WriteAllText(scriptPath + ".new", "text of another writer");
        File.WriteAllText(scriptPath + ".previous", "text of another writer");
        string pathInMissingFolder = Path.Combine(folder.ContentBuilderPath, "missing", "depot_100031.vdf");

        Assert.That(
            () => AllOrNothingFileWrite.WriteAll(new[] { (scriptPath, "new text"), (pathInMissingFolder, "new text") }),
            Throws.InstanceOf<DirectoryNotFoundException>());

        Assert.That(File.ReadAllText(scriptPath), Is.EqualTo("old text"));
        Assert.That(File.ReadAllText(scriptPath + ".new"), Is.EqualTo("text of another writer"));
        Assert.That(File.ReadAllText(scriptPath + ".previous"), Is.EqualTo("text of another writer"));
        Assert.That(Directory.GetFiles(folder.ScriptsPath), Has.Length.EqualTo(3));
    }

    [Test]
    public void WriteAll_KeepsTheOldTextWhenTheTargetIsReadOnly()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string filePath = Path.Combine(folder.ScriptsPath, "app_100030.vdf");
        File.WriteAllText(filePath, "old text");
        File.SetAttributes(filePath, FileAttributes.ReadOnly);
        try
        {
            Assert.That(() => AllOrNothingFileWrite.WriteAll(new[] { (filePath, "new text") }),
                Throws.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>());

            Assert.That(File.ReadAllText(filePath), Is.EqualTo("old text"));
            Assert.That(Directory.GetFiles(folder.ScriptsPath), Is.EqualTo(new[] { filePath }));
        }
        finally
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }
    }

    [Test]
    public void WriteAll_KeepsTheOldTextWhenAnotherProgramHoldsTheTarget()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string filePath = Path.Combine(folder.ScriptsPath, "app_100030.vdf");
        File.WriteAllText(filePath, "old text");

        using (new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.That(() => AllOrNothingFileWrite.WriteAll(new[] { (filePath, "new text") }),
                Throws.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>());
        }

        Assert.That(File.ReadAllText(filePath), Is.EqualTo("old text"));
        Assert.That(Directory.GetFiles(folder.ScriptsPath), Is.EqualTo(new[] { filePath }));
    }

    [Test]
    public void WriteAll_RefusesAFolderAtTheTargetPath()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string firstPath = Path.Combine(folder.ScriptsPath, "app_100030.vdf");
        string folderAtTargetPath = Path.Combine(folder.ScriptsPath, "depot_100031.vdf");
        Directory.CreateDirectory(folderAtTargetPath);

        Assert.That(
            () => AllOrNothingFileWrite.WriteAll(new[] { (firstPath, "new text"), (folderAtTargetPath, "new text") }),
            Throws.InstanceOf<IOException>());

        Assert.That(Directory.Exists(folderAtTargetPath), Is.True);
        Assert.That(Directory.GetFiles(folder.ScriptsPath), Is.Empty);
    }

    [Test]
    public void WriteAll_UndoesOnlyItsOwnChangesWhenALaterFileCannotBeReplaced()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string replacedPath = Path.Combine(folder.ScriptsPath, "app_100030.vdf");
        string newPath = Path.Combine(folder.ScriptsPath, "depot_100031.vdf");
        string heldPath = Path.Combine(folder.ScriptsPath, "depot_100032.vdf");
        File.WriteAllText(replacedPath, "old app text", Encoding.Unicode);
        File.WriteAllText(newPath + ".previous", "text of another writer");
        File.WriteAllText(heldPath, "old depot text");
        byte[] replacedBytesBefore = File.ReadAllBytes(replacedPath);
        string accessRulesBefore = TestFileMetadata.GiveOwnAccessRules(replacedPath);

        using (new FileStream(heldPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.That(
                () => AllOrNothingFileWrite.WriteAll(new[]
                {
                    (replacedPath, "new app text"), (newPath, "new depot text"), (heldPath, "new depot text"),
                }),
                Throws.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>());
        }

        Assert.That(File.ReadAllBytes(replacedPath), Is.EqualTo(replacedBytesBefore));
        Assert.That(TestFileMetadata.AccessRulesOf(replacedPath), Is.EqualTo(accessRulesBefore));
        Assert.That(File.Exists(newPath), Is.False);
        Assert.That(File.ReadAllText(newPath + ".previous"), Is.EqualTo("text of another writer"));
        Assert.That(File.ReadAllText(heldPath), Is.EqualTo("old depot text"));
        Assert.That(Directory.GetFiles(folder.ScriptsPath).OrderBy(path => path),
            Is.EqualTo(new[] { replacedPath, newPath + ".previous", heldPath }));
    }

    [Test]
    public void WriteAll_PutsTheOriginalBackWhenAReplacementFailsAfterMovingItAside()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string filePath = Path.Combine(folder.ScriptsPath, "settings.json");
        File.WriteAllText(filePath, "old text", Encoding.Unicode);
        byte[] bytesBefore = File.ReadAllBytes(filePath);

        Assert.That(
            () => AllOrNothingFileWrite.WriteAll(new[] { (filePath, "new text") }, FailAfterMovingTheOriginalAside),
            Throws.InstanceOf<IOException>().With.Message.EqualTo(MovedAsideErrorText));

        Assert.That(File.ReadAllBytes(filePath), Is.EqualTo(bytesBefore));
        Assert.That(Directory.GetFiles(folder.ScriptsPath), Is.EqualTo(new[] { filePath }));
    }

    [Test]
    public void WriteAll_KeepsTheBackupAndNamesItWhenTheOriginalCannotBePutBack()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string filePath = Path.Combine(folder.ScriptsPath, "settings.json");
        File.WriteAllText(filePath, "old text", Encoding.Unicode);
        byte[] bytesBefore = File.ReadAllBytes(filePath);
        FolderThatDeniesNewFiles? deniedNewFiles = null;
        IOException? writeError;
        try
        {
            writeError = Assert.Throws<IOException>(() => AllOrNothingFileWrite.WriteAll(
                new[] { (filePath, "new text") },
                (stagedFilePath, replacedFilePath, backupFilePath) =>
                {
                    File.Move(replacedFilePath, backupFilePath);
                    deniedNewFiles = new FolderThatDeniesNewFiles(folder.ScriptsPath);

                    throw new IOException(MovedAsideErrorText);
                }));
        }
        finally
        {
            deniedNewFiles?.Dispose();
        }

        string keptBackupPath = Directory.GetFiles(folder.ScriptsPath, "settings.json.*.previous").Single();
        Assert.That(writeError!.Message, Does.Contain(MovedAsideErrorText).And.Contain(keptBackupPath));
        Assert.That(File.ReadAllBytes(keptBackupPath), Is.EqualTo(bytesBefore));
        Assert.That(File.Exists(filePath), Is.False);
        Assert.That(Directory.GetFiles(folder.ScriptsPath), Is.EqualTo(new[] { keptBackupPath }));
    }

    [Test]
    public void WriteAll_RestoresWhatItCanAndKeepsTheBackupOfAFileItCannotPutBack()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string heldAfterReplacePath = Path.Combine(folder.ScriptsPath, "app_100030.vdf");
        string restoredPath = Path.Combine(folder.ScriptsPath, "depot_100031.vdf");
        string movedAsidePath = Path.Combine(folder.ScriptsPath, "depot_100032.vdf");
        File.WriteAllText(heldAfterReplacePath, "old app text", Encoding.Unicode);
        File.WriteAllText(restoredPath, "old first depot text");
        File.WriteAllText(movedAsidePath, "old second depot text");
        byte[] heldBytesBefore = File.ReadAllBytes(heldAfterReplacePath);
        (string, string)[] files =
        {
            (heldAfterReplacePath, "new app text"), (restoredPath, "new first depot text"),
            (movedAsidePath, "new second depot text"),
        };
        FileStream? handleThatBlocksThePutBack = null;
        IOException? writeError;
        try
        {
            writeError = Assert.Throws<IOException>(() => AllOrNothingFileWrite.WriteAll(files,
                (stagedFilePath, replacedFilePath, backupFilePath) =>
                {
                    if (replacedFilePath != movedAsidePath)
                    {
                        File.Replace(stagedFilePath, replacedFilePath, backupFilePath, ignoreMetadataErrors: false);

                        return;
                    }

                    handleThatBlocksThePutBack = new FileStream(heldAfterReplacePath, FileMode.Open, FileAccess.Read,
                        FileShare.Read);
                    FailAfterMovingTheOriginalAside(stagedFilePath, replacedFilePath, backupFilePath);
                }));
        }
        finally
        {
            handleThatBlocksThePutBack?.Dispose();
        }

        string keptBackupPath = Directory.GetFiles(folder.ScriptsPath, "app_100030.vdf.*.previous").Single();
        Assert.That(writeError!.Message, Does.Contain(keptBackupPath));
        Assert.That(File.ReadAllBytes(keptBackupPath), Is.EqualTo(heldBytesBefore));
        Assert.That(File.ReadAllText(heldAfterReplacePath), Is.EqualTo("new app text"));
        Assert.That(File.ReadAllText(restoredPath), Is.EqualTo("old first depot text"));
        Assert.That(File.ReadAllText(movedAsidePath), Is.EqualTo("old second depot text"));
        Assert.That(Directory.GetFiles(folder.ScriptsPath), Has.Length.EqualTo(4));

        File.Delete(heldAfterReplacePath);
        File.Move(keptBackupPath, heldAfterReplacePath);
        Assert.That(File.ReadAllBytes(heldAfterReplacePath), Is.EqualTo(heldBytesBefore));
        AllOrNothingFileWrite.WriteAll(files);

        Assert.That(File.ReadAllText(heldAfterReplacePath), Is.EqualTo("new app text"));
        Assert.That(File.ReadAllText(restoredPath), Is.EqualTo("new first depot text"));
        Assert.That(File.ReadAllText(movedAsidePath), Is.EqualTo("new second depot text"));
        Assert.That(Directory.GetFiles(folder.ScriptsPath).OrderBy(path => path),
            Is.EqualTo(new[] { heldAfterReplacePath, restoredPath, movedAsidePath }));
    }

    private static void FailAfterMovingTheOriginalAside(string stagedFilePath, string filePath, string backupFilePath)
    {
        File.Move(filePath, backupFilePath);

        throw new IOException(MovedAsideErrorText);
    }
}
