namespace DepotUploader;

internal static class BatchResultLine
{
    public static string Text(IReadOnlyList<(string GameName, UploadOutcome Outcome)> games, DateTime endTime)
    {
        int finishedCount = games.Count(game => game.Outcome == UploadOutcome.Finished);

        return $"{finishedCount} of {games.Count} games finished at {endTime:HH:mm}"
            + NamesOfGamesWith(UploadOutcome.Failed, "Failed", games)
            + NamesOfGamesWith(UploadOutcome.Stopped, "Stopped", games)
            + NamesOfGamesWith(UploadOutcome.NotStarted, "Not started", games);
    }

    private static string NamesOfGamesWith(UploadOutcome outcome, string outcomeLabel,
        IReadOnlyList<(string GameName, UploadOutcome Outcome)> games)
    {
        string[] gameNames = games.Where(game => game.Outcome == outcome).Select(game => game.GameName).ToArray();

        return gameNames.Length == 0 ? string.Empty : $". {outcomeLabel}: {string.Join(", ", gameNames)}";
    }
}
