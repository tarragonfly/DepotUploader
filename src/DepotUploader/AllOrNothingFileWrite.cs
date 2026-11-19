namespace DepotUploader;

internal static class AllOrNothingFileWrite
{
    public static void WriteAll(IReadOnlyList<(string FilePath, string Text)> files)
    {
        List<string> replacedFilePaths = new List<string>();
        try
        {
            foreach ((string filePath, string text) in files)
            {
                File.WriteAllText(NewTextPath(filePath), text);
            }

            foreach ((string filePath, _) in files)
            {
                if (File.Exists(filePath))
                {
                    File.Replace(NewTextPath(filePath), filePath, PreviousTextPath(filePath));
                }
                else
                {
                    File.Move(NewTextPath(filePath), filePath);
                }

                replacedFilePaths.Add(filePath);
            }
        }
        catch
        {
            foreach (string filePath in replacedFilePaths)
            {
                RestorePreviousText(filePath);
            }

            foreach ((string filePath, _) in files)
            {
                File.Delete(NewTextPath(filePath));
            }

            throw;
        }

        foreach (string filePath in replacedFilePaths)
        {
            DeleteWhenPossible(PreviousTextPath(filePath));
        }
    }

    private static string NewTextPath(string filePath)
    {
        return filePath + ".new";
    }

    private static string PreviousTextPath(string filePath)
    {
        return filePath + ".previous";
    }

    private static void RestorePreviousText(string filePath)
    {
        if (File.Exists(PreviousTextPath(filePath)))
        {
            File.Move(PreviousTextPath(filePath), filePath, overwrite: true);
        }
        else
        {
            File.Delete(filePath);
        }
    }

    private static void DeleteWhenPossible(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }
    }
}
