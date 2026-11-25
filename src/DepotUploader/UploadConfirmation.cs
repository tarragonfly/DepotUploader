namespace DepotUploader;

internal static class UploadConfirmation
{
    public static string Question(IReadOnlyList<GameUpload> uploads)
    {
        if (uploads.Count == 1)
        {
            return uploads[0].UploadsNothing
                ? $"Run a preview build of {uploads[0].GameName} ({uploads[0].AppId})? Nothing is uploaded."
                : $"Upload {uploads[0].GameName} ({uploads[0].AppId}) to Steam?";
        }

        int previewCount = uploads.Count(upload => upload.UploadsNothing);
        int uploadCount = uploads.Count - previewCount;
        if (previewCount == 0)
        {
            return $"Upload {GameCountText(uploadCount)} to Steam?";
        }

        if (uploadCount == 0)
        {
            return $"Run a preview build of {GameCountText(previewCount)}? Nothing is uploaded.";
        }

        return $"Upload {GameCountText(uploadCount)} to Steam and run a preview build of "
            + $"{GameCountText(previewCount)}?";
    }

    public static string Details(IReadOnlyList<GameUpload> uploads,
        IReadOnlyList<ContentFolderSummary[]> contentByGame)
    {
        return string.Join(Environment.NewLine, uploads.Count == 1
            ? DetailLinesOfOneGame(uploads[0], contentByGame[0])
            : DetailLinesOfSeveralGames(uploads, contentByGame));
    }

    private static List<string> DetailLinesOfOneGame(GameUpload upload, ContentFolderSummary[] content)
    {
        List<string> lines = new List<string>
        {
            $"Description: {upload.Description}",
            upload.LiveBranch.Length == 0 ? "Set live: no" : $"Set live on branch: {upload.LiveBranch}",
        };
        for (int i = 0; i < upload.Depots.Count; i++)
        {
            lines.Add($"Depot {upload.Depots[i].DepotId}: {upload.Depots[i].ContentFolder}");
            lines.Add($"    {content[i]}");
        }

        return lines;
    }

    private static List<string> DetailLinesOfSeveralGames(IReadOnlyList<GameUpload> uploads,
        IReadOnlyList<ContentFolderSummary[]> contentByGame)
    {
        List<string> lines = new List<string>();
        for (int gameIndex = 0; gameIndex < uploads.Count; gameIndex++)
        {
            GameUpload upload = uploads[gameIndex];
            lines.Add($"{upload.GameName} ({upload.AppId}): {UploadSettingsInOneLine(upload)}");
            for (int i = 0; i < upload.Depots.Count; i++)
            {
                lines.Add($"    Depot {upload.Depots[i].DepotId}: {upload.Depots[i].ContentFolder}, "
                    + contentByGame[gameIndex][i]);
            }
        }

        return lines;
    }

    private static string UploadSettingsInOneLine(GameUpload upload)
    {
        if (upload.UploadsNothing)
        {
            return "preview build, nothing is uploaded";
        }

        string description = upload.Description.Length == 0 ? "no description" : $"\"{upload.Description}\"";
        string liveBranch = upload.LiveBranch.Length == 0 ? "not set live" : $"set live on {upload.LiveBranch}";

        return $"{description}, {liveBranch}";
    }

    private static string GameCountText(int gameCount)
    {
        return gameCount == 1 ? "1 game" : $"{gameCount} games";
    }
}
