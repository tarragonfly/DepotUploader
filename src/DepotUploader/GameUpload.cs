namespace DepotUploader;

internal sealed record GameUpload(string GameName, string AppId, string AppScriptFileName, string Description,
    string LiveBranch, bool UploadsNothing, string AppScriptCopyPath, string BuildLogPath,
    IReadOnlyList<(string DepotId, string ContentFolder)> Depots)
{
    public static GameUpload FromScripts(AppBuildScript game, string gameName, string folderForScriptCopies)
    {
        if (!AddGameForm.IsNumber(game.AppId))
        {
            throw new InvalidOperationException(
                $"The app ID \"{game.AppId}\" in {Path.GetFileName(game.FilePath)} is not a number.");
        }

        (string DepotId, string ContentFolder)[] depots = game.Depots
            .Select(depot => (depot.DepotId, game.AbsoluteContentFolder(depot)))
            .ToArray();
        foreach ((string depotId, string contentFolder) in depots)
        {
            if (contentFolder.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Depot {depotId} of {gameName} ({game.AppId}) has no content folder.");
            }
        }

        return new GameUpload(gameName, game.AppId, Path.GetFileName(game.FilePath), game.Description,
            game.LiveBranch, game.UploadsNothing, game.WriteCopiesWithAbsolutePaths(folderForScriptCopies),
            game.BuildLogPath, depots);
    }

    public static string? WhyTwoUploadsWouldSetTheSameBranchLive(IReadOnlyList<GameUpload> uploads)
    {
        GameUpload[] uploadsThatSetABranchLive = uploads
            .Where(upload => !upload.UploadsNothing && upload.LiveBranch.Trim().Length > 0)
            .ToArray();
        foreach (GameUpload upload in uploadsThatSetABranchLive)
        {
            GameUpload? laterUploadToTheSameBranch = uploadsThatSetABranchLive
                .SkipWhile(other => !ReferenceEquals(other, upload))
                .Skip(1)
                .FirstOrDefault(other => other.AppId.TrimStart('0') == upload.AppId.TrimStart('0')
                    && string.Equals(other.LiveBranch.Trim(), upload.LiveBranch.Trim(),
                        StringComparison.OrdinalIgnoreCase));
            if (laterUploadToTheSameBranch is not null)
            {
                return $"{upload.AppScriptFileName} and {laterUploadToTheSameBranch.AppScriptFileName} both upload "
                    + $"app {upload.AppId} and set it live on branch {upload.LiveBranch.Trim()}. "
                    + "The second build would replace the first on that branch. Untick one of the two.";
            }
        }

        return null;
    }

    public static async Task<ContentFolderSummary[][]?> DescribeContentUnlessStoppedAsync(
        IReadOnlyList<GameUpload> uploads, CancellationToken stopRequested)
    {
        Task<ContentFolderSummary[][]> described = Task.Run(
            () => uploads.Select(upload => upload.DescribeContent(stopRequested)).ToArray(), stopRequested);
        TaskCompletionSource<bool> stopped =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (stopRequested.Register(() => stopped.TrySetResult(true)))
        {
            await Task.WhenAny(described, stopped.Task);
        }

        if (stopRequested.IsCancellationRequested)
        {
            _ = described.ContinueWith(task => task.Exception, TaskContinuationOptions.OnlyOnFaulted);

            return null;
        }

        return await described;
    }

    public ContentFolderSummary[] DescribeContent(CancellationToken cancellation)
    {
        return Depots.Select(depot => DescribeContentOfDepot(depot.DepotId, depot.ContentFolder, cancellation))
            .ToArray();
    }

    private ContentFolderSummary DescribeContentOfDepot(string depotId, string contentFolder,
        CancellationToken cancellation)
    {
        string depotIdAndGame = $"{depotId} of {GameName} ({AppId})";
        ContentFolderSummary summary;
        try
        {
            summary = ContentFolderSummary.Describe(contentFolder, cancellation);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                $"The files of depot {depotIdAndGame} in \"{contentFolder}\" could not be counted. {exception.Message}",
                exception);
        }

        if (!summary.HasFiles && !Directory.Exists(contentFolder))
        {
            throw new IOException($"Depot {depotIdAndGame} has no content folder at \"{contentFolder}\".");
        }

        return summary;
    }
}
