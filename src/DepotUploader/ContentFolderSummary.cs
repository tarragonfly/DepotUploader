using System.Globalization;

namespace DepotUploader;

internal sealed record ContentFolderSummary(string Text, bool HasFiles)
{
    public static ContentFolderSummary Describe(string folder)
    {
        if (folder.Length == 0)
        {
            return new ContentFolderSummary("No folder set", HasFiles: false);
        }

        if (!Directory.Exists(folder))
        {
            return new ContentFolderSummary("Folder not found", HasFiles: false);
        }

        int fileCount = 0;
        long totalBytes = 0;
        DateTime newestWrite = DateTime.MinValue;
        foreach (FileInfo file in new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            fileCount++;
            totalBytes += file.Length;
            if (file.LastWriteTime > newestWrite)
            {
                newestWrite = file.LastWriteTime;
            }
        }

        if (fileCount == 0)
        {
            return new ContentFolderSummary("Empty folder", HasFiles: false);
        }

        string size = totalBytes >= 1L << 30
            ? (totalBytes / (double)(1L << 30)).ToString("0.00", CultureInfo.InvariantCulture) + " GB"
            : (totalBytes / (double)(1L << 20)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";

        return new ContentFolderSummary($"{fileCount} files, {size}, newest {newestWrite:yyyy-MM-dd HH:mm}",
            HasFiles: true);
    }

    public static bool FolderHasFiles(string folder)
    {
        try
        {
            return folder.Length > 0 && Directory.Exists(folder)
                && Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Any();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public override string ToString()
    {
        return Text;
    }
}
