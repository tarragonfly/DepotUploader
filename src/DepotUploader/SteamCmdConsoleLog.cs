using System.Text;
using System.Text.RegularExpressions;

namespace DepotUploader;

internal sealed class SteamCmdConsoleLog
{
    private const string ScanProgressDot = ".";

    private static readonly Regex TimestampOfConsoleWrite = new Regex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\] ?");

    private readonly string _path;
    private readonly string _pathAfterRotation;
    private long _lengthAlreadyRead;
    private volatile bool _hasReturnedLines;

    public SteamCmdConsoleLog(string steamCmdPath)
    {
        string logsFolder = Path.Combine(Path.GetDirectoryName(steamCmdPath)!, "logs");
        _path = Path.Combine(logsFolder, "console_log.txt");
        _pathAfterRotation = Path.Combine(logsFolder, "console_log.previous.txt");
        _lengthAlreadyRead = File.Exists(_path) ? new FileInfo(_path).Length : 0;
    }

    public bool HasReturnedLines => _hasReturnedLines;

    public List<string> ReadAppendedLines()
    {
        List<string> lines = new List<string>();
        try
        {
            bool steamCmdRotatedTheLog = _lengthAlreadyRead > 0
                && (!File.Exists(_path) || new FileInfo(_path).Length < _lengthAlreadyRead);
            if (steamCmdRotatedTheLog)
            {
                lines.AddRange(LinesToShow(Encoding.UTF8.GetString(
                    ReadWholeLines(_pathAfterRotation, _lengthAlreadyRead))));
                _lengthAlreadyRead = 0;
            }

            byte[] wholeLines = ReadWholeLines(_path, _lengthAlreadyRead);
            _lengthAlreadyRead += wholeLines.Length;
            lines.AddRange(LinesToShow(Encoding.UTF8.GetString(wholeLines)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        if (lines.Count > 0)
        {
            _hasReturnedLines = true;
        }

        return lines;
    }

    internal static List<string> LinesToShow(string appendedText)
    {
        List<string> lines = new List<string>();
        foreach (string consoleWrite in appendedText.Split('\n'))
        {
            string line = TimestampOfConsoleWrite.Replace(consoleWrite, string.Empty).Trim();
            if (line.Length > 0 && line != ScanProgressDot)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    private static byte[] ReadWholeLines(string path, long position)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<byte>();
        }

        using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length < position)
        {
            return Array.Empty<byte>();
        }

        stream.Seek(position, SeekOrigin.Begin);
        byte[] appended = new byte[stream.Length - position];
        int appendedLength = 0;
        while (appendedLength < appended.Length)
        {
            int readLength = stream.Read(appended, appendedLength, appended.Length - appendedLength);
            if (readLength == 0)
            {
                break;
            }

            appendedLength += readLength;
        }

        int lengthOfWholeLines = appendedLength == 0
            ? 0
            : Array.LastIndexOf(appended, (byte)'\n', appendedLength - 1, appendedLength) + 1;
        Array.Resize(ref appended, lengthOfWholeLines);

        return appended;
    }
}
