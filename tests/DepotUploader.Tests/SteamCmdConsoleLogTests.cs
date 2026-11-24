namespace DepotUploader.Tests;

public class SteamCmdConsoleLogTests
{
    [Test]
    public void LinesToShow_RemovesTheTimestampOfEachWriteAndSkipsScanProgressDotsAndEmptyLines()
    {
        List<string> lines = SteamCmdConsoleLog.LinesToShow(
            "[2026-11-24 00:56:29] [2026-11-24 00:56:29]: Building depot 1429151...\n\r\n"
            + "[2026-11-24 00:56:37] Scanning content\r\n"
            + "[2026-11-24 00:56:38] .\r\n"
            + "[2026-11-24 00:56:38]  281.2MB (11%)\r\n"
            + "[2026-11-24 00:56:51] \r\n"
            + "[2026-11-24 00:56:51] Uploading content...\r\n");

        Assert.That(lines, Is.EqualTo(new[]
        {
            "[2026-11-24 00:56:29]: Building depot 1429151...",
            "Scanning content",
            "281.2MB (11%)",
            "Uploading content...",
        }));
    }

    [Test]
    public void ReadAppendedLines_ReturnsALineOnlyAfterItsLineFeedIsWritten()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string consoleLogPath = WriteConsoleLog(folder, "[2026-11-24 00:50:00] Line of an earlier upload\r\n");
        SteamCmdConsoleLog consoleLog = new SteamCmdConsoleLog(Path.Combine(folder.ScriptsPath, "steamcmd.exe"));

        File.AppendAllText(consoleLogPath, "[2026-11-24 00:56:51] Uploading");
        List<string> linesBeforeLineFeed = consoleLog.ReadAppendedLines();
        File.AppendAllText(consoleLogPath, " content...\r\n");
        List<string> linesAfterLineFeed = consoleLog.ReadAppendedLines();

        Assert.That(linesBeforeLineFeed, Is.Empty);
        Assert.That(linesAfterLineFeed, Is.EqualTo(new[] { "Uploading content..." }));
        Assert.That(consoleLog.ReadAppendedLines(), Is.Empty);
    }

    [Test]
    public void ReadAppendedLines_ReadsAShorterFileThatReplacedTheLogFromItsStart()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string consoleLogPath = WriteConsoleLog(folder,
            "[2026-11-24 00:50:00] Line of an earlier upload\r\n[2026-11-24 00:50:01] Another line\r\n");
        SteamCmdConsoleLog consoleLog = new SteamCmdConsoleLog(Path.Combine(folder.ScriptsPath, "steamcmd.exe"));

        File.WriteAllText(consoleLogPath, "[2026-11-24 00:56:27] Client version: 1794537082\r\n");

        Assert.That(consoleLog.ReadAppendedLines(), Is.EqualTo(new[] { "Client version: 1794537082" }));
    }

    [Test]
    public void ReadAppendedLines_ReturnsTheLinesWrittenBeforeARotationAndTheLinesAfterIt()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string consoleLogPath = WriteConsoleLog(folder, "[2026-11-24 00:50:00] Line of an earlier upload\r\n");
        SteamCmdConsoleLog consoleLog = new SteamCmdConsoleLog(Path.Combine(folder.ScriptsPath, "steamcmd.exe"));

        File.AppendAllText(consoleLogPath, "[2026-11-24 00:56:51] ERROR! Failed to upload\r\n");
        File.Move(consoleLogPath, Path.Combine(Path.GetDirectoryName(consoleLogPath)!, "console_log.previous.txt"));
        File.WriteAllText(consoleLogPath, "[2026-11-24 00:56:52] Unloading Steam API...\r\n");

        Assert.That(consoleLog.ReadAppendedLines(),
            Is.EqualTo(new[] { "ERROR! Failed to upload", "Unloading Steam API..." }));
    }

    [Test]
    public void ReadAppendedLines_ReturnsTheLinesWrittenBeforeARotationWhenNoNewLogExistsYet()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string consoleLogPath = WriteConsoleLog(folder, "[2026-11-24 00:50:00] Line of an earlier upload\r\n");
        SteamCmdConsoleLog consoleLog = new SteamCmdConsoleLog(Path.Combine(folder.ScriptsPath, "steamcmd.exe"));

        File.AppendAllText(consoleLogPath, "[2026-11-24 00:56:51] ERROR! Failed to upload\r\n");
        File.Move(consoleLogPath, Path.Combine(Path.GetDirectoryName(consoleLogPath)!, "console_log.previous.txt"));
        List<string> linesBeforeTheNewLogExists = consoleLog.ReadAppendedLines();
        File.WriteAllText(consoleLogPath, "[2026-11-24 00:56:52] Unloading Steam API...\r\n");

        Assert.That(linesBeforeTheNewLogExists, Is.EqualTo(new[] { "ERROR! Failed to upload" }));
        Assert.That(consoleLog.ReadAppendedLines(), Is.EqualTo(new[] { "Unloading Steam API..." }));
    }

    private static string WriteConsoleLog(TemporaryContentBuilderFolder folder, string text)
    {
        string consoleLogPath = Path.Combine(folder.ScriptsPath, "logs", "console_log.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(consoleLogPath)!);
        File.WriteAllText(consoleLogPath, text);

        return consoleLogPath;
    }
}
