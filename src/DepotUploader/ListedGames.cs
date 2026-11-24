namespace DepotUploader;

internal static class ListedGames
{
    public static List<AppBuildScript> MatchingFilterInSortOrder(IEnumerable<AppBuildScript> games,
        Func<AppBuildScript, string> listText, string filterText, GameSortOrder sortOrder)
    {
        IEnumerable<AppBuildScript> gamesByName = games
            .Where(game => MatchesFilter(listText(game), filterText))
            .OrderBy(listText, StringComparer.CurrentCultureIgnoreCase);

        return sortOrder switch
        {
            GameSortOrder.LastBuildFirst => gamesByName.OrderByDescending(game => game.LastBuildTimeUtc).ToList(),
            GameSortOrder.RecentlyAddedFirst => gamesByName.OrderByDescending(game => game.FileCreationTimeUtc).ToList(),
            _ => gamesByName.ToList(),
        };
    }

    private static bool MatchesFilter(string listText, string filterText)
    {
        return listText.IndexOf(filterText.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0;
    }
}
