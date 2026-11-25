using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DepotUploader;

internal sealed record ContentFolderSummary(string Text, bool HasFiles)
{
    public int FileCount { get; private init; }

    public long TotalBytes { get; private init; }

    public DateTime NewestWriteUtc { get; private init; }

    public int UnreadableSubfolderCount { get; private init; }

    public string HashOfPathSizeAndWriteTimeOfEachFile { get; private init; } = string.Empty;

    public string TextWithExactSizeAndTime => (HasFiles
            ? $"{FileCount} files, {TotalBytes} bytes, newest {NewestWriteUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
            + UnreadableSubfoldersText(UnreadableSubfolderCount)
            : Text)
        + (HashOfPathSizeAndWriteTimeOfEachFile.Length == 0
            ? string.Empty
            : $", file list {HashOfPathSizeAndWriteTimeOfEachFile.Substring(0, 8)}");

    public static ContentFolderSummary Describe(string folder, CancellationToken cancellation = default)
    {
        if (folder.Length == 0)
        {
            return new ContentFolderSummary("No folder set", HasFiles: false);
        }

        if (!Directory.Exists(folder))
        {
            return new ContentFolderSummary("Folder not found", HasFiles: false);
        }

        DirectoryInfo contentFolder = new DirectoryInfo(folder);
        int fileCount = 0;
        long totalBytes = 0;
        DateTime newestWriteUtc = DateTime.MinValue;
        int unreadableSubfolderCount = 0;
        List<string> pathSizeAndWriteTimeOfEachFile = new List<string>();
        foreach (FileInfo file in FilesInReadableFolders(contentFolder, unreadableSubfolder =>
                 {
                     unreadableSubfolderCount++;
                     pathSizeAndWriteTimeOfEachFile.Add(
                         $"{PathInside(contentFolder, unreadableSubfolder)}|not readable");
                 }, cancellation))
        {
            fileCount++;
            totalBytes += file.Length;
            if (file.LastWriteTimeUtc > newestWriteUtc)
            {
                newestWriteUtc = file.LastWriteTimeUtc;
            }

            pathSizeAndWriteTimeOfEachFile.Add(
                $"{PathInside(contentFolder, file)}|{file.Length}|{file.LastWriteTimeUtc.Ticks}");
        }

        string hashOfPathSizeAndWriteTimeOfEachFile = pathSizeAndWriteTimeOfEachFile.Count == 0
            ? string.Empty
            : HashOfLinesInOrdinalOrder(pathSizeAndWriteTimeOfEachFile);
        if (fileCount == 0)
        {
            return new ContentFolderSummary("Empty folder" + UnreadableSubfoldersText(unreadableSubfolderCount),
                HasFiles: false)
            {
                UnreadableSubfolderCount = unreadableSubfolderCount,
                HashOfPathSizeAndWriteTimeOfEachFile = hashOfPathSizeAndWriteTimeOfEachFile,
            };
        }

        string size = totalBytes >= 1L << 30
            ? (totalBytes / (double)(1L << 30)).ToString("0.00", CultureInfo.InvariantCulture) + " GB"
            : (totalBytes / (double)(1L << 20)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";

        return new ContentFolderSummary(
            $"{fileCount} files, {size}, newest {newestWriteUtc.ToLocalTime():yyyy-MM-dd HH:mm}"
            + UnreadableSubfoldersText(unreadableSubfolderCount), HasFiles: true)
        {
            FileCount = fileCount,
            TotalBytes = totalBytes,
            NewestWriteUtc = newestWriteUtc,
            UnreadableSubfolderCount = unreadableSubfolderCount,
            HashOfPathSizeAndWriteTimeOfEachFile = hashOfPathSizeAndWriteTimeOfEachFile,
        };
    }

    public static ContentFolderSummary DescribeOrGiveReadError(string folder,
        CancellationToken cancellation = default)
    {
        try
        {
            return Describe(folder, cancellation);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ContentFolderSummary(exception.Message, HasFiles: false);
        }
    }

    public static bool FolderHasFiles(string folder)
    {
        try
        {
            return folder.Length > 0 && Directory.Exists(folder)
                && FilesInReadableFolders(new DirectoryInfo(folder), _ => { }, CancellationToken.None).Any();
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

    private static IEnumerable<FileInfo> FilesInReadableFolders(DirectoryInfo contentFolder,
        Action<DirectoryInfo> onUnreadableSubfolder, CancellationToken cancellation)
    {
        Stack<DirectoryInfo> foldersToRead = new Stack<DirectoryInfo>();
        foldersToRead.Push(contentFolder);
        while (foldersToRead.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            DirectoryInfo folderToRead = foldersToRead.Pop();
            FileSystemInfo[] filesAndSubfolders;
            try
            {
                filesAndSubfolders = folderToRead.GetFileSystemInfos();
            }
            catch (Exception exception) when (!ReferenceEquals(folderToRead, contentFolder)
                && exception is IOException or UnauthorizedAccessException)
            {
                onUnreadableSubfolder(folderToRead);
                continue;
            }

            foreach (FileSystemInfo fileOrSubfolder in filesAndSubfolders)
            {
                cancellation.ThrowIfCancellationRequested();
                if (fileOrSubfolder is DirectoryInfo subfolder)
                {
                    foldersToRead.Push(subfolder);
                }
                else if (fileOrSubfolder is FileInfo file)
                {
                    yield return file;
                }
            }
        }
    }

    private static string PathInside(DirectoryInfo contentFolder, FileSystemInfo fileOrSubfolder)
    {
        return fileOrSubfolder.FullName.Substring(contentFolder.FullName.Length);
    }

    private static string HashOfLinesInOrdinalOrder(List<string> lines)
    {
        lines.Sort(StringComparer.Ordinal);
        using SHA256Cng sha256 = new SHA256Cng();
        foreach (string line in lines)
        {
            byte[] lineBytes = Encoding.UTF8.GetBytes(line + "\n");
            sha256.TransformBlock(lineBytes, 0, lineBytes.Length, null, 0);
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

        return BitConverter.ToString(sha256.Hash).Replace("-", string.Empty);
    }

    private static string UnreadableSubfoldersText(int unreadableSubfolderCount)
    {
        return unreadableSubfolderCount switch
        {
            0 => string.Empty,
            1 => ", 1 subfolder not readable",
            _ => $", {unreadableSubfolderCount} subfolders not readable",
        };
    }
}
