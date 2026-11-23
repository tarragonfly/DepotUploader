namespace DepotUploader;

internal sealed class BuildScriptFile
{
    private const string ExtendedLengthPrefix = @"\\?\";

    private string? _fileTextWhenLastReadOrWritten;

    private BuildScriptFile(string filePath, KeyValuesNode root, string? fileText)
    {
        FilePath = filePath;
        Root = root;
        _fileTextWhenLastReadOrWritten = fileText;
    }

    public string FilePath { get; }

    public KeyValuesNode Root { get; private set; }

    public bool HasUnsavedEdits => _fileTextWhenLastReadOrWritten is null
        || ToScriptText() != KeyValuesText.Write(KeyValuesText.Parse(_fileTextWhenLastReadOrWritten));

    public static BuildScriptFile Read(string filePath)
    {
        string text = File.ReadAllText(filePath);

        return new BuildScriptFile(filePath, KeyValuesText.Parse(text), text);
    }

    public static BuildScriptFile NotYetWritten(string filePath, KeyValuesNode root)
    {
        return new BuildScriptFile(filePath, root, null);
    }

    public void UndoUnsavedEdits()
    {
        if (_fileTextWhenLastReadOrWritten is not null)
        {
            Root = KeyValuesText.Parse(_fileTextWhenLastReadOrWritten);
        }
    }

    public string ToScriptText()
    {
        return KeyValuesText.Write(Root);
    }

    public string AbsolutePath(string pathInScript)
    {
        try
        {
            string path = PathFromScriptFolder(pathInScript);

            return path.StartsWith(ExtendedLengthPrefix, StringComparison.Ordinal)
                ? WithoutRelativeFolderNames(path)
                : Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return pathInScript;
        }
    }

    private string PathFromScriptFolder(string pathInScript)
    {
        string scriptFolder = Path.GetDirectoryName(Path.GetFullPath(FilePath))!;
        if (StartsWithOneSeparator(pathInScript))
        {
            return Path.Combine(Path.GetPathRoot(scriptFolder), pathInScript.Substring(1));
        }

        if (IsRelativeToADrive(pathInScript))
        {
            string drive = pathInScript.Substring(0, 2);
            string pathOnDrive = pathInScript.Substring(2);

            string prefixOfScriptFolder = scriptFolder.StartsWith(ExtendedLengthPrefix, StringComparison.Ordinal)
                ? ExtendedLengthPrefix
                : string.Empty;

            return scriptFolder.Substring(prefixOfScriptFolder.Length)
                .StartsWith(drive, StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(scriptFolder, pathOnDrive)
                : $"{prefixOfScriptFolder}{drive}\\{pathOnDrive}";
        }

        return Path.Combine(scriptFolder, pathInScript);
    }

    private static string WithoutRelativeFolderNames(string extendedLengthPath)
    {
        string root = Path.GetPathRoot(extendedLengthPath);
        List<string> names = new List<string>();
        foreach (string name in extendedLengthPath.Substring(root.Length)
                     .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (name == "..")
            {
                if (names.Count > 0)
                {
                    names.RemoveAt(names.Count - 1);
                }
            }
            else if (name.Length > 0 && name != ".")
            {
                names.Add(name);
            }
        }

        return names.Count == 0
            ? root
            : root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar
            + string.Join(Path.DirectorySeparatorChar.ToString(), names);
    }

    private static bool StartsWithOneSeparator(string path)
    {
        return path.Length > 0 && IsSeparator(path[0]) && (path.Length == 1 || !IsSeparator(path[1]));
    }

    private static bool IsRelativeToADrive(string path)
    {
        return path.Length >= 2 && path[1] == ':' && (path.Length == 2 || !IsSeparator(path[2]));
    }

    private static bool IsSeparator(char character)
    {
        return character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar;
    }

    public bool FileChangedAfterLastReadOrWrite()
    {
        if (!File.Exists(FilePath))
        {
            return _fileTextWhenLastReadOrWritten is not null;
        }

        return File.ReadAllText(FilePath) != _fileTextWhenLastReadOrWritten;
    }

    public void RememberWrittenText(string text)
    {
        _fileTextWhenLastReadOrWritten = text;
    }
}
