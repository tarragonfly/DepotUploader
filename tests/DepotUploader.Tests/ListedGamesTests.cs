namespace DepotUploader.Tests;

public class ListedGamesTests
{
    private static readonly Dictionary<string, string> NamesByAppId = new Dictionary<string, string>
    {
        ["100010"] = "Neon Garden",
        ["100020"] = "amber Circuit",
        ["100030"] = "Paper Kingdom",
        ["100040"] = "Clockwork Harbor",
        ["100050"] = "Zinc Lantern",
        ["100|060<"] = "Amber Harbor",
    };

    [Test]
    public void MatchingFilterInSortOrder_ListsTheGamesWhoseNameOrAppIdContainsTheFilterText()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        List<AppBuildScript> games = WriteFourGames(folder);

        Assert.That(ListedAppIds(games, " KING ", GameSortOrder.NameAToZ), Is.EqualTo(new[] { "100030" }));
        Assert.That(ListedAppIds(games, "0002", GameSortOrder.NameAToZ), Is.EqualTo(new[] { "100020" }));
        Assert.That(ListedAppIds(games, "no such game", GameSortOrder.NameAToZ), Is.Empty);
    }

    [Test]
    public void MatchingFilterInSortOrder_SortsByNameWithoutRegardToCase()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        List<AppBuildScript> games = WriteFourGames(folder);

        Assert.That(ListedAppIds(games, string.Empty, GameSortOrder.NameAToZ),
            Is.EqualTo(new[] { "100020", "100040", "100010", "100030" }));
    }

    [Test]
    public void MatchingFilterInSortOrder_PutsTheLastBuiltGameFirstAndGamesWithoutABuildLogLastByName()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        List<AppBuildScript> games = WriteFourGames(folder);
        WriteBuildLog(games[0], new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc));
        WriteBuildLog(games[2], new DateTime(2026, 11, 20, 18, 30, 0, DateTimeKind.Utc));

        Assert.That(ListedAppIds(games, string.Empty, GameSortOrder.LastBuildFirst),
            Is.EqualTo(new[] { "100030", "100010", "100020", "100040" }));
    }

    [Test]
    public void MatchingFilterInSortOrder_ReadsTheBuildLogInTheBuildOutputFolderOfEachAppScript()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\content\\100010");
        folder.WriteScript("app_100050.vdf",
            "\"appbuild\" { \"appid\" \"100050\" \"buildoutput\" \"..\\logs of 100050\\\" \"depots\" { } }");
        List<AppBuildScript> games = new ContentBuilderFolder(folder.ContentBuilderPath).LoadAppBuildScripts(out _);
        WriteBuildLog(games[0], new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc));
        WriteBuildLog(games[1], new DateTime(2026, 11, 20, 18, 30, 0, DateTimeKind.Utc));

        Assert.That(games[1].BuildLogPath,
            Is.EqualTo(Path.Combine(folder.ContentBuilderPath, "logs of 100050", "app_build_100050.log")));
        Assert.That(ListedAppIds(games, string.Empty, GameSortOrder.LastBuildFirst),
            Is.EqualTo(new[] { "100050", "100010" }));
    }

    [Test]
    public void MatchingFilterInSortOrder_ListsAGameWhoseBuildLogPathIsNotAPathAfterTheGamesWithABuildLog()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        folder.WriteAppAndDepotScriptsLikeSteamPipeGui("100010", "100011", "E:\\content\\100010");
        folder.WriteScript("app_100060.vdf",
            "\"appbuild\" { \"appid\" \"100|060<\" \"buildoutput\" \"..\\out|put\" \"depots\" { } }");
        List<AppBuildScript> games = new ContentBuilderFolder(folder.ContentBuilderPath).LoadAppBuildScripts(out _);
        WriteBuildLog(games[0], new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc));

        Assert.That(ListedAppIds(games, string.Empty, GameSortOrder.LastBuildFirst),
            Is.EqualTo(new[] { "100010", "100|060<" }));
        Assert.That(ListedAppIds(games, string.Empty, GameSortOrder.RecentlyAddedFirst), Has.Exactly(2).Items);
    }

    [Test]
    public void MatchingFilterInSortOrder_PutsTheMostRecentlyCreatedAppScriptFirst()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        List<AppBuildScript> games = WriteFourGames(folder);
        File.SetCreationTimeUtc(games[0].FilePath, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetCreationTimeUtc(games[1].FilePath, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetCreationTimeUtc(games[2].FilePath, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetCreationTimeUtc(games[3].FilePath, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.That(ListedAppIds(games, string.Empty, GameSortOrder.RecentlyAddedFirst),
            Is.EqualTo(new[] { "100020", "100040", "100010", "100030" }));
    }

    private static List<AppBuildScript> WriteFourGames(TemporaryContentBuilderFolder folder)
    {
        foreach (string appId in new[] { "100010", "100020", "100030", "100040" })
        {
            folder.WriteAppAndDepotScriptsLikeSteamPipeGui(appId, appId.Substring(0, 5) + "1", "E:\\content\\" + appId);
        }

        return new ContentBuilderFolder(folder.ContentBuilderPath).LoadAppBuildScripts(out _);
    }

    private static void WriteBuildLog(AppBuildScript game, DateTime writeTimeUtc)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(game.BuildLogPath)!);
        File.WriteAllText(game.BuildLogPath, "build log");
        File.SetLastWriteTimeUtc(game.BuildLogPath, writeTimeUtc);
    }

    private static IEnumerable<string> ListedAppIds(List<AppBuildScript> games, string filterText,
        GameSortOrder sortOrder)
    {
        return ListedGames.MatchingFilterInSortOrder(games, game => $"{NamesByAppId[game.AppId]} ({game.AppId})",
            filterText, sortOrder).Select(game => game.AppId);
    }
}
