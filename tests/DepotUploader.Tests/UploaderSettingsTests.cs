namespace DepotUploader.Tests;

public class UploaderSettingsTests
{
    [Test]
    public void Load_ReturnsTheValuesSaveWrote()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings", "settings.json");
        UploaderSettings saved = new UploaderSettings
        {
            ContentBuilderPath = @"C:\steamworks\sdk\tools\ContentBuilder",
            SteamLogin = "builder_account",
        };
        saved.GameNamesByAppId["100030"] = "Sample Game";

        saved.Save(settingsPath);
        UploaderSettings loaded = UploaderSettings.Load(settingsPath, out string? loadError);

        Assert.That(loadError, Is.Null);
        Assert.That(loaded.ContentBuilderPath, Is.EqualTo(saved.ContentBuilderPath));
        Assert.That(loaded.SteamLogin, Is.EqualTo(saved.SteamLogin));
        Assert.That(loaded.GameNamesByAppId, Is.EqualTo(saved.GameNamesByAppId));
    }

    [Test]
    public void Save_WritesIndentedJsonThatLoadReadsBackWithEveryCharacter()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");
        UploaderSettings saved = new UploaderSettings
        {
            ContentBuilderPath = @"C:\steamworks\sdk\tools\ContentBuilder",
            SteamLogin = "builder \"quoted\" <account> & it's\ttabbed",
        };
        saved.GameNamesByAppId["100030"] = "Sample Game: Za\u017c\u00f3\u0142\u0107 /Date(1)/ \u65e5\u672c\u8a9e";
        saved.GameNamesByAppId["200040"] = string.Empty;

        saved.Save(settingsPath);
        string[] lines = File.ReadAllLines(settingsPath);
        UploaderSettings loaded = UploaderSettings.Load(settingsPath, out string? loadError);

        Assert.That(lines[0], Is.EqualTo("{"));
        Assert.That(lines[1], Is.EqualTo("  \"ContentBuilderPath\": \"C:\\\\steamworks\\\\sdk\\\\tools\\\\ContentBuilder\","));
        Assert.That(lines[2], Does.StartWith("  \"SteamLogin\": \""));
        Assert.That(lines[3], Is.EqualTo("  \"GameSortOrder\": \"NameAToZ\","));
        Assert.That(lines[4], Is.EqualTo("  \"GameNamesByAppId\": {"));
        Assert.That(lines[5], Does.StartWith("    \"100030\": \""));
        Assert.That(lines[6], Is.EqualTo("    \"200040\": \"\""));
        Assert.That(lines[7], Is.EqualTo("  }"));
        Assert.That(lines[8], Is.EqualTo("}"));
        Assert.That(lines, Has.Length.EqualTo(9));
        Assert.That(loadError, Is.Null);
        Assert.That(loaded.SteamLogin, Is.EqualTo(saved.SteamLogin));
        Assert.That(loaded.GameNamesByAppId, Is.EqualTo(saved.GameNamesByAppId));
    }

    [Test]
    public void Save_WritesAnEmptyJsonObjectWhenNoGameHasAName()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");

        new UploaderSettings { SteamLogin = "builder_account" }.Save(settingsPath);
        UploaderSettings loaded = UploaderSettings.Load(settingsPath, out string? loadError);

        Assert.That(File.ReadAllLines(settingsPath)[4], Is.EqualTo("  \"GameNamesByAppId\": {}"));
        Assert.That(loadError, Is.Null);
        Assert.That(loaded.SteamLogin, Is.EqualTo("builder_account"));
        Assert.That(loaded.GameNamesByAppId, Is.Empty);
    }

    [Test]
    public void Load_StartsEmptyWhenThereIsNoSettingsFile()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();

        UploaderSettings inExistingFolder = UploaderSettings.Load(
            Path.Combine(folder.ContentBuilderPath, "settings.json"), out string? existingFolderLoadError);
        UploaderSettings inMissingFolder = UploaderSettings.Load(
            Path.Combine(folder.ContentBuilderPath, "missing", "settings.json"), out string? missingFolderLoadError);

        Assert.That(existingFolderLoadError, Is.Null);
        Assert.That(missingFolderLoadError, Is.Null);
        Assert.That(inExistingFolder.MustNotOverwriteFile, Is.False);
        Assert.That(inMissingFolder.MustNotOverwriteFile, Is.False);
        Assert.That(inExistingFolder.SteamLogin, Is.Empty);
    }

    [Test]
    public void Load_ReadsASettingsFileWrittenByVersion1_0_0()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");
        File.WriteAllText(settingsPath,
            "{\r\n"
            + "  \"ContentBuilderPath\": \"C:\\\\steamworks\\\\sdk\\\\tools\\\\ContentBuilder\",\r\n"
            + "  \"SteamLogin\": \"builder_account\",\r\n"
            + "  \"GameNamesByAppId\": {\r\n"
            + "    \"100030\": \"Sample Game\"\r\n"
            + "  }\r\n"
            + "}");

        UploaderSettings loaded = UploaderSettings.Load(settingsPath, out string? loadError);

        Assert.That(loadError, Is.Null);
        Assert.That(loaded.ContentBuilderPath, Is.EqualTo(@"C:\steamworks\sdk\tools\ContentBuilder"));
        Assert.That(loaded.SteamLogin, Is.EqualTo("builder_account"));
        Assert.That(loaded.GameName("100030"), Is.EqualTo("Sample Game"));
        Assert.That(loaded.GameSortOrder, Is.EqualTo(GameSortOrder.NameAToZ));
    }

    [Test]
    public void Load_ReturnsTheGameSortOrderSaveWrote()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");

        new UploaderSettings { GameSortOrder = GameSortOrder.RecentlyAddedFirst }.Save(settingsPath);
        UploaderSettings loaded = UploaderSettings.Load(settingsPath, out string? loadError);

        Assert.That(loadError, Is.Null);
        Assert.That(loaded.GameSortOrder, Is.EqualTo(GameSortOrder.RecentlyAddedFirst));
    }

    [Test]
    public void Load_SortsByNameWhenTheGameSortOrderIsNotOneThisVersionKnows()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");
        File.WriteAllText(settingsPath, "{ \"SteamLogin\": \"builder_account\", \"GameSortOrder\": \"MostDepotsFirst\" }");

        UploaderSettings loaded = UploaderSettings.Load(settingsPath, out string? loadError);

        Assert.That(loadError, Is.Null);
        Assert.That(loaded.MustNotOverwriteFile, Is.False);
        Assert.That(loaded.SteamLogin, Is.EqualTo("builder_account"));
        Assert.That(loaded.GameSortOrder, Is.EqualTo(GameSortOrder.NameAToZ));
    }

    [Test]
    public void Load_ReplacesNullValuesFromAHandEditedFile()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");
        File.WriteAllText(settingsPath, "{ \"ContentBuilderPath\": null, \"GameNamesByAppId\": null }");
        string namesPath = Path.Combine(folder.ContentBuilderPath, "names.json");
        File.WriteAllText(namesPath, "{ \"GameNamesByAppId\": { \"100030\": null } }");

        UploaderSettings withoutNames = UploaderSettings.Load(settingsPath, out _);
        UploaderSettings withNullName = UploaderSettings.Load(namesPath, out _);

        Assert.That(withoutNames.ContentBuilderPath, Is.Empty);
        Assert.That(withoutNames.GameName("100030"), Is.EqualTo("App 100030"));
        Assert.That(withNullName.GameName("100030"), Is.EqualTo("App 100030"));
    }

    [Test]
    public void Load_IgnoresUnknownMembersAndReadsEscapedTextAsWritten()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");
        File.WriteAllText(settingsPath,
            "{ \"__type\": 0, \"Window\": { \"Size\": [ 1100, 760 ], \"Title\": \"} ]\" }, \"Version\": 2, "
            + "\"Beta\": true, \"SteamLogin\": \"builder_account\", "
            + "\"GameNamesByAppId\": { \"__type\": null, \"100030\": \"\\/Date(0)\\/\", "
            + "\"200040\": \"a \\\"quoted\\\" \\\\ name\", \"type\": \"Type Game\", \"item\": \"Item Game\" } }");

        UploaderSettings loaded = UploaderSettings.Load(settingsPath, out string? loadError);

        Assert.That(loadError, Is.Null);
        Assert.That(loaded.MustNotOverwriteFile, Is.False);
        Assert.That(loaded.SteamLogin, Is.EqualTo("builder_account"));
        Assert.That(loaded.GameName("100030"), Is.EqualTo("/Date(0)/"));
        Assert.That(loaded.GameName("200040"), Is.EqualTo("a \"quoted\" \\ name"));
        Assert.That(loaded.GameName("__type"), Is.EqualTo("App __type"));
        Assert.That(loaded.GameName("type"), Is.EqualTo("Type Game"));
        Assert.That(loaded.GameName("item"), Is.EqualTo("Item Game"));
        Assert.That(loaded.GameNamesByAppId, Has.Count.EqualTo(5));
    }

    [Test]
    public void Load_ReadsTheLastOfTwoMembersWithTheSameName()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");
        File.WriteAllText(settingsPath,
            "{ \"SteamLogin\": \"first_account\", \"SteamLogin\": \"builder_account\", "
            + "\"GameNamesByAppId\": { \"100030\": \"First Name\", \"100030\": \"Sample Game\" } }");

        UploaderSettings loaded = UploaderSettings.Load(settingsPath, out string? loadError);

        Assert.That(loadError, Is.Null);
        Assert.That(loaded.SteamLogin, Is.EqualTo("builder_account"));
        Assert.That(loaded.GameName("100030"), Is.EqualTo("Sample Game"));
    }

    [TestCase("{ \"SteamLogin\": \"builder_acc")]
    [TestCase("{ \"SteamLogin\": 5 }")]
    [TestCase("[ \"builder_account\" ]")]
    [TestCase("{ \"SteamLogin\": \"builder_account\"")]
    [TestCase("{ \"SteamLogin\": \"builder_account\",")]
    [TestCase("{ \"GameNamesByAppId\": { \"100030\": \"Sample Game\" }")]
    [TestCase("{ \"GameNamesByAppId\": [ \"Sample Game\" ] }")]
    [TestCase("{ \"SteamLogin\": \"builder_account\" } and more text")]
    [TestCase("{ \"SteamLogin\": \"builder_account\" }{}")]
    [TestCase("{ \"SteamLogin\": 1, \"SteamLogin\": \"builder_account\" }")]
    [TestCase("{ \"GameNamesByAppId\": { \"100030\": 1, \"100030\": \"Sample Game\" } }")]
    [TestCase("{ \"GameNamesByAppId\": { \"100030\": \"Sample \\ud800 Game\" } }")]
    [TestCase("{ \"SteamLogin\": \"builder_account\" }null")]
    [TestCase("{ SteamLogin: \"builder_account\" }")]
    [TestCase("{ 'SteamLogin': 'builder_account' }")]
    [TestCase("")]
    [TestCase("  \r\n")]
    public void Load_LeavesAnInvalidFileUntouchedAndRefusesToSave(string invalidText)
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");
        File.WriteAllText(settingsPath, invalidText);

        UploaderSettings loaded = UploaderSettings.Load(settingsPath, out string? loadError);

        Assert.That(loaded.SteamLogin, Is.Empty);
        Assert.That(loaded.MustNotOverwriteFile, Is.True);
        Assert.That(loadError, Does.Contain(settingsPath).And.Contain("are not saved"));
        Assert.That(() => loaded.Save(settingsPath), Throws.InstanceOf<IOException>());
        Assert.That(File.ReadAllText(settingsPath), Is.EqualTo(invalidText));
        Assert.That(Directory.GetFiles(folder.ContentBuilderPath), Is.EqualTo(new[] { settingsPath }));
    }

    [Test]
    public void Load_RefusesToOverwriteAFileItCouldNotRead()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");
        new UploaderSettings { SteamLogin = "builder_account" }.Save(settingsPath);
        string savedText = File.ReadAllText(settingsPath);

        UploaderSettings loaded;
        string? loadError;
        using (new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            loaded = UploaderSettings.Load(settingsPath, out loadError);
        }

        Assert.That(loaded.MustNotOverwriteFile, Is.True);
        Assert.That(loadError, Does.Contain(settingsPath).And.Contain("are not saved"));
        Assert.That(() => loaded.Save(settingsPath), Throws.InstanceOf<IOException>());
        Assert.That(File.ReadAllText(settingsPath), Is.EqualTo(savedText));
        Assert.That(Directory.GetFiles(folder.ContentBuilderPath), Is.EqualTo(new[] { settingsPath }));
    }

    [Test]
    public void Load_ReadsTheSettingsInAFolderThatDeniesNewFiles()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");
        new UploaderSettings { SteamLogin = "builder_account" }.Save(settingsPath);
        string savedText = File.ReadAllText(settingsPath);
        using FolderThatDeniesNewFiles deniedNewFiles = new FolderThatDeniesNewFiles(folder.ContentBuilderPath);

        UploaderSettings loaded = UploaderSettings.Load(settingsPath, out string? loadError);

        Assert.That(loadError, Is.Null);
        Assert.That(loaded.SteamLogin, Is.EqualTo("builder_account"));
        Assert.That(loaded.MustNotOverwriteFile, Is.False);
        Assert.That(File.ReadAllText(settingsPath), Is.EqualTo(savedText));
        Assert.That(Directory.GetFiles(folder.ContentBuilderPath), Is.EqualTo(new[] { settingsPath }));
    }

    [TestCase("settings.json.previous")]
    [TestCase("settings.json.0123456789abcdef0123456789abcdef.previous")]
    public void Load_StartsEmptyAndRefusesToSaveWhenOnlyABackupExists(string backupFileName)
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");
        string backupPath = Path.Combine(folder.ContentBuilderPath, backupFileName);
        File.WriteAllText(backupPath, "{ \"SteamLogin\": \"builder_account\" }");

        UploaderSettings loaded = UploaderSettings.Load(settingsPath, out string? loadError);

        Assert.That(loaded.SteamLogin, Is.Empty);
        Assert.That(loaded.MustNotOverwriteFile, Is.True);
        Assert.That(loadError, Does.Contain(backupPath));
        Assert.That(() => loaded.Save(settingsPath), Throws.InstanceOf<IOException>());
        Assert.That(File.ReadAllText(backupPath), Is.EqualTo("{ \"SteamLogin\": \"builder_account\" }"));
        Assert.That(Directory.GetFiles(folder.ContentBuilderPath), Is.EqualTo(new[] { backupPath }));
    }

    [Test]
    public void Save_KeepsTheBackupAsTheOnlyCopyUntilItIsRenamedBack()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");
        new UploaderSettings { SteamLogin = "builder_account" }.Save(settingsPath);
        byte[] savedBytes = File.ReadAllBytes(settingsPath);
        UploaderSettings window = UploaderSettings.Load(settingsPath, out _);
        window.SteamLogin = "other_account";
        FolderThatDeniesNewFiles? deniedNewFiles = null;
        try
        {
            Assert.That(
                () => AllOrNothingFileWrite.WriteAll(new[] { (settingsPath, "new text") },
                    (stagedFilePath, replacedFilePath, backupFilePath) =>
                    {
                        File.Move(replacedFilePath, backupFilePath);
                        deniedNewFiles = new FolderThatDeniesNewFiles(folder.ContentBuilderPath);

                        throw new IOException("Unable to move the replacement file to the file to be replaced.");
                    }),
                Throws.InstanceOf<IOException>());
        }
        finally
        {
            deniedNewFiles?.Dispose();
        }

        string backupPath = Directory.GetFiles(folder.ContentBuilderPath, "settings.json.*.previous").Single();
        Assert.That(() => window.Save(settingsPath),
            Throws.InstanceOf<IOException>().With.Message.Contains(backupPath));
        UploaderSettings reopened = UploaderSettings.Load(settingsPath, out string? loadError);
        Assert.That(loadError, Does.Contain(backupPath));
        Assert.That(reopened.MustNotOverwriteFile, Is.True);
        Assert.That(() => reopened.Save(settingsPath), Throws.InstanceOf<IOException>());
        Assert.That(File.ReadAllBytes(backupPath), Is.EqualTo(savedBytes));
        Assert.That(Directory.GetFiles(folder.ContentBuilderPath), Is.EqualTo(new[] { backupPath }));

        File.Move(backupPath, settingsPath);
        window.Save(settingsPath);

        Assert.That(UploaderSettings.Load(settingsPath, out _).SteamLogin, Is.EqualTo("other_account"));
        Assert.That(Directory.GetFiles(folder.ContentBuilderPath), Is.EqualTo(new[] { settingsPath }));
    }

    [Test]
    public void Save_AfterAFailedSaveWritesTheSameValues()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");
        new UploaderSettings { SteamLogin = "builder_account" }.Save(settingsPath);
        byte[] savedBytes = File.ReadAllBytes(settingsPath);
        UploaderSettings settings = UploaderSettings.Load(settingsPath, out _);
        settings.SteamLogin = "other_account";
        settings.GameNamesByAppId["100030"] = "Sample Game";

        using (new FolderThatDeniesNewFiles(folder.ContentBuilderPath))
        {
            Assert.That(() => settings.Save(settingsPath), Throws.InstanceOf<UnauthorizedAccessException>());
            Assert.That(File.ReadAllBytes(settingsPath), Is.EqualTo(savedBytes));
        }

        using (new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.That(() => settings.Save(settingsPath),
                Throws.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>());
        }

        Assert.That(File.ReadAllBytes(settingsPath), Is.EqualTo(savedBytes));
        settings.Save(settingsPath);

        UploaderSettings saved = UploaderSettings.Load(settingsPath, out _);
        Assert.That(saved.SteamLogin, Is.EqualTo("other_account"));
        Assert.That(saved.GameName("100030"), Is.EqualTo("Sample Game"));
        Assert.That(Directory.GetFiles(folder.ContentBuilderPath), Is.EqualTo(new[] { settingsPath }));
    }

    [Test]
    public void Save_KeepsTheAccessRulesAndAlternateStreamOfTheSettingsFile()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");
        new UploaderSettings { SteamLogin = "builder_account" }.Save(settingsPath);
        TestFileMetadata.WriteAlternateStream(settingsPath);
        string accessRulesBefore = TestFileMetadata.GiveOwnAccessRules(settingsPath);

        new UploaderSettings { SteamLogin = "other_account" }.Save(settingsPath);

        Assert.That(UploaderSettings.Load(settingsPath, out _).SteamLogin, Is.EqualTo("other_account"));
        Assert.That(TestFileMetadata.AccessRulesOf(settingsPath), Is.EqualTo(accessRulesBefore));
        Assert.That(TestFileMetadata.ReadAlternateStream(settingsPath),
            Is.EqualTo(TestFileMetadata.AlternateStreamBytes));
        Assert.That(Directory.GetFiles(folder.ContentBuilderPath), Is.EqualTo(new[] { settingsPath }));
    }

    [Test]
    public void Save_KeepsTheEncryptionOfTheSettingsFile()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string settingsPath = Path.Combine(folder.ContentBuilderPath, "settings.json");
        new UploaderSettings { SteamLogin = "builder_account" }.Save(settingsPath);
        if (!TestFileMetadata.TryEncrypt(settingsPath))
        {
            Assert.Ignore("EFS is not available on this Windows edition or volume.");
        }

        new UploaderSettings { SteamLogin = "other_account" }.Save(settingsPath);

        Assert.That(UploaderSettings.Load(settingsPath, out _).SteamLogin, Is.EqualTo("other_account"));
        Assert.That(File.GetAttributes(settingsPath).HasFlag(FileAttributes.Encrypted), Is.True);
        Assert.That(Directory.GetFiles(folder.ContentBuilderPath), Is.EqualTo(new[] { settingsPath }));
    }
}
