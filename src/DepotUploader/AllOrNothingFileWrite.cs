using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;

namespace DepotUploader;

internal static class AllOrNothingFileWrite
{
    private const int MoveFileReplaceExisting = 1;
    private const int MoveFileCopyAllowed = 2;
    private const int AccessDeniedError = 5;

    private static readonly UTF8Encoding Utf8WithoutByteOrderMark = new UTF8Encoding(false);

    public static void WriteAll(IReadOnlyList<(string FilePath, string Text)> files)
    {
        WriteAll(files, ReplaceKeepingBackup);
    }

    internal static void WriteAll(IReadOnlyList<(string FilePath, string Text)> files,
        Action<string, string, string> replaceFile)
    {
        List<StagedFile> stagedFiles = new List<StagedFile>();
        List<StagedFile> committedFiles = new List<StagedFile>();
        StagedFile? fileBeingReplaced = null;
        try
        {
            foreach ((string filePath, string text) in files)
            {
                stagedFiles.Add(Stage(filePath, text));
            }

            foreach (StagedFile stagedFile in stagedFiles)
            {
                if (stagedFile.ReplacesExistingFile)
                {
                    fileBeingReplaced = stagedFile;
                    replaceFile(stagedFile.StagedFilePath, stagedFile.FilePath, stagedFile.BackupFilePath);
                    fileBeingReplaced = null;
                }
                else
                {
                    File.Move(stagedFile.StagedFilePath, stagedFile.FilePath);
                }

                committedFiles.Add(stagedFile);
            }
        }
        catch (Exception writeError)
        {
            List<string> filesNotPutBack = new List<string>();
            if (fileBeingReplaced is not null)
            {
                PutBackAfterFailedReplace(fileBeingReplaced, filesNotPutBack);
            }

            for (int fileIndex = committedFiles.Count - 1; fileIndex >= 0; fileIndex--)
            {
                Undo(committedFiles[fileIndex], filesNotPutBack);
            }

            foreach (StagedFile stagedFile in stagedFiles)
            {
                DeleteWhenPossible(stagedFile.StagedFilePath);
            }

            if (filesNotPutBack.Count > 0)
            {
                throw new IOException($"{writeError.Message} {string.Join(" ", filesNotPutBack)}", writeError);
            }

            throw;
        }

        foreach (StagedFile stagedFile in stagedFiles)
        {
            if (stagedFile.ReplacesExistingFile)
            {
                DeleteWhenPossible(stagedFile.BackupFilePath);
            }
        }
    }

    private static void ReplaceKeepingBackup(string stagedFilePath, string filePath, string backupFilePath)
    {
        File.Replace(stagedFilePath, filePath, backupFilePath, ignoreMetadataErrors: false);
    }

    private static StagedFile Stage(string filePath, string text)
    {
        bool replacesExistingFile = IsExistingFile(filePath);
        string stagingId = Guid.NewGuid().ToString("N");
        string stagedFilePath = $"{filePath}.{stagingId}.new";
        string backupFilePath = $"{filePath}.{stagingId}.previous";
        if (PathExists(backupFilePath))
        {
            throw new IOException($"{backupFilePath} already exists.");
        }

        new FileStream(stagedFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None).Dispose();
        try
        {
            if (replacesExistingFile)
            {
                GiveSameAccessRules(filePath, stagedFilePath);
                if (File.GetAttributes(filePath).HasFlag(FileAttributes.Encrypted))
                {
                    File.Encrypt(stagedFilePath);
                }
            }

            using FileStream stagedFile = new FileStream(stagedFilePath, FileMode.Open, FileAccess.Write,
                FileShare.None);
            byte[] textBytes = Utf8WithoutByteOrderMark.GetBytes(text);
            stagedFile.Write(textBytes, 0, textBytes.Length);
            stagedFile.Flush(flushToDisk: true);
        }
        catch
        {
            DeleteWhenPossible(stagedFilePath);

            throw;
        }

        return new StagedFile(filePath, stagedFilePath, backupFilePath, replacesExistingFile);
    }

    private static bool IsExistingFile(string filePath)
    {
        if (!PathExists(filePath))
        {
            return false;
        }

        if (File.GetAttributes(filePath).HasFlag(FileAttributes.Directory))
        {
            throw new IOException($"{filePath} is a folder.");
        }

        return true;
    }

    public static bool PathExists(string path)
    {
        try
        {
            File.GetAttributes(path);

            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static void GiveSameAccessRules(string fromFilePath, string toFilePath)
    {
        try
        {
            string accessRules = AccessRulesOf(fromFilePath);
            if (AccessRulesOf(toFilePath) == accessRules)
            {
                return;
            }

            FileSecurity security = new FileSecurity();
            security.SetSecurityDescriptorSddlForm(accessRules, AccessControlSections.Access);
            new FileInfo(toFilePath).SetAccessControl(security);
        }
        catch (Exception exception) when (exception is not (IOException or UnauthorizedAccessException))
        {
            throw new IOException(
                $"The access rules of {Path.GetFileName(fromFilePath)} could not be copied. {exception.Message}",
                exception);
        }
    }

    private static string AccessRulesOf(string filePath)
    {
        return new FileInfo(filePath).GetAccessControl(AccessControlSections.Access)
            .GetSecurityDescriptorSddlForm(AccessControlSections.Access);
    }

    private static void PutBackAfterFailedReplace(StagedFile fileBeingReplaced, List<string> filesNotPutBack)
    {
        try
        {
            if (!PathExists(fileBeingReplaced.BackupFilePath))
            {
                return;
            }

            if (PathExists(fileBeingReplaced.FilePath))
            {
                filesNotPutBack.Add($"{Path.GetFileName(fileBeingReplaced.FilePath)} and its backup "
                    + $"{fileBeingReplaced.BackupFilePath} both exist.");

                return;
            }

            File.Move(fileBeingReplaced.BackupFilePath, fileBeingReplaced.FilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            filesNotPutBack.Add(KeptBackupText(fileBeingReplaced));
        }
    }

    private static void Undo(StagedFile committedFile, List<string> filesNotPutBack)
    {
        try
        {
            if (committedFile.ReplacesExistingFile)
            {
                MoveOverExistingFile(committedFile.BackupFilePath, committedFile.FilePath);
            }
            else
            {
                File.Delete(committedFile.FilePath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            filesNotPutBack.Add(committedFile.ReplacesExistingFile
                ? KeptBackupText(committedFile)
                : $"{committedFile.FilePath} could not be removed.");
        }
    }

    private static void MoveOverExistingFile(string sourceFilePath, string targetFilePath)
    {
        if (MoveFileEx(Path.GetFullPath(sourceFilePath), Path.GetFullPath(targetFilePath),
                MoveFileReplaceExisting | MoveFileCopyAllowed))
        {
            return;
        }

        int error = Marshal.GetLastWin32Error();
        string message = $"{new Win32Exception(error).Message} ({sourceFilePath})";
        if (error == AccessDeniedError)
        {
            throw new UnauthorizedAccessException(message);
        }

        throw new IOException(message, unchecked((int)0x80070000 | error));
    }

    private static string KeptBackupText(StagedFile stagedFile)
    {
        return $"The previous {Path.GetFileName(stagedFile.FilePath)} could not be put back and is kept as "
            + $"{stagedFile.BackupFilePath}.";
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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string existingFilePath, string newFilePath, int flags);

    private sealed record StagedFile(string FilePath, string StagedFilePath, string BackupFilePath,
        bool ReplacesExistingFile);
}
