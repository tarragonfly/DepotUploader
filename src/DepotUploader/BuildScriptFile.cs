namespace DepotUploader;

internal sealed class BuildScriptFile
{
    private string? _fileTextWhenLastReadOrWritten;

    private BuildScriptFile(string filePath, KeyValuesNode root, string? fileText)
    {
        FilePath = filePath;
        Root = root;
        _fileTextWhenLastReadOrWritten = fileText;
    }

    public string FilePath { get; }

    public KeyValuesNode Root { get; }

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

    public string ToScriptText()
    {
        return KeyValuesText.Write(Root);
    }

    public string AbsolutePath(string pathInScript)
    {
        return Path.GetFullPath(pathInScript, Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
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
