using System.Globalization;

namespace DepotUploader;

internal static class ContentFolderSummary
{
    public static string Describe(string folder)
    {
        if (folder.Length == 0)
        {
            return "No folder set";
        }

        if (!Directory.Exists(folder))
        {
            return "Folder not found";
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
            return "Empty folder";
        }

        string size = totalBytes >= 1L << 30
            ? (totalBytes / (double)(1L << 30)).ToString("0.00", CultureInfo.InvariantCulture) + " GB"
            : (totalBytes / (double)(1L << 20)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";

        return $"{fileCount} files, {size}, newest {newestWrite:yyyy-MM-dd HH:mm}";
    }
}
