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
        saved.GameNamesByAppId["100030"] = "Dungeon Tavern";

        saved.Save(settingsPath);
        UploaderSettings loaded = UploaderSettings.Load(settingsPath);

        Assert.That(loaded.ContentBuilderPath, Is.EqualTo(saved.ContentBuilderPath));
        Assert.That(loaded.SteamLogin, Is.EqualTo(saved.SteamLogin));
        Assert.That(loaded.GameNamesByAppId, Is.EqualTo(saved.GameNamesByAppId));
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
            + "    \"100030\": \"Dungeon Tavern\"\r\n"
            + "  }\r\n"
            + "}");

        UploaderSettings loaded = UploaderSettings.Load(settingsPath);

        Assert.That(loaded.ContentBuilderPath, Is.EqualTo(@"C:\steamworks\sdk\tools\ContentBuilder"));
        Assert.That(loaded.SteamLogin, Is.EqualTo("builder_account"));
        Assert.That(loaded.GameName("100030"), Is.EqualTo("Dungeon Tavern"));
    }
}
