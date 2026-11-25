using System.Collections.Concurrent;
using System.Diagnostics;

namespace DepotUploader.Tests;

public class SteamCmdTests
{
    [Test]
    public async Task RunAppBuildAsync_PassesLoginAndScriptAndReturnsOutputAndExitCode()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string steamCmdStandIn = folder.WriteScript("steamcmd-stand-in.cmd",
            "@echo %*\r\n@echo second line 1>&2\r\n@exit /b 3\r\n");
        string appScriptPath = Path.Combine(folder.ScriptsPath, "app_100030.vdf");
        ConcurrentQueue<string> lines = new ConcurrentQueue<string>();

        int exitCode = await SteamCmd.RunAppBuildAsync(steamCmdStandIn, "builder_account", appScriptPath,
            lines.Enqueue, CancellationToken.None);

        Assert.That(exitCode, Is.EqualTo(3));
        Assert.That(lines, Has.Some.EqualTo($"+login builder_account +run_app_build {appScriptPath} +quit"));
        Assert.That(lines, Has.Some.StartsWith("second line"));
    }

    [TestCase("builder_account", "builder_account")]
    [TestCase("", "\"\"")]
    [TestCase(@"C:\my scripts\app_100030.vdf", @"""C:\my scripts\app_100030.vdf""")]
    [TestCase(@"C:\my scripts\", @"""C:\my scripts\\""")]
    [TestCase(@"say ""hi""", @"""say \""hi\""""")]
    [TestCase(@"ends with \""", @"""ends with \\\""""")]
    public void QuotedArguments_QuotesAnArgumentTheWayWindowsSplitsACommandLine(string argument, string expected)
    {
        Assert.That(SteamCmd.QuotedArguments("+login", argument), Is.EqualTo("+login " + expected));
    }

    [Test]
    public async Task RunAppBuildAsync_PassesAScriptPathWithSpacesAsOneArgument()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string steamCmdStandIn = folder.WriteScript("steamcmd-stand-in.cmd", "@echo [%4]\r\n");
        string appScriptPath = Path.Combine(folder.ScriptsPath, "my scripts", "app_100030.vdf");
        ConcurrentQueue<string> lines = new ConcurrentQueue<string>();

        await SteamCmd.RunAppBuildAsync(steamCmdStandIn, "builder_account", appScriptPath, lines.Enqueue,
            CancellationToken.None);

        Assert.That(lines, Has.Some.EqualTo($"[\"{appScriptPath}\"]"));
    }

    [Test]
    public void RunAppBuildAsync_ForwardsAConsoleLogLineWhileSteamCmdStillRuns()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string steamCmdStandIn = folder.WriteScript("steamcmd-stand-in.cmd",
            "@mkdir logs\r\n"
            + "@echo [2026-11-24 00:56:51] Uploading content...>>logs\\console_log.txt\r\n"
            + "@ping -n 30 127.0.0.1 >nul\r\n");
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        using ManualResetEventSlim forwardedConsoleLogLine = new ManualResetEventSlim();

        Task<int> run = SteamCmd.RunAppBuildAsync(steamCmdStandIn, "builder_account", "app_100030.vdf", line =>
        {
            if (line == "Uploading content...")
            {
                forwardedConsoleLogLine.Set();
            }
        }, cancellation.Token);

        Assert.That(forwardedConsoleLogLine.Wait(TimeSpan.FromSeconds(10)), Is.True);
        Assert.That(run.IsCompleted, Is.False);
        cancellation.Cancel();
        Assert.That(async () => await run, Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task RunAppBuildAsync_AfterAConsoleLogLineForwardsStderrAndNoStdout()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string consoleLogPath = Path.Combine(folder.ScriptsPath, "logs", "console_log.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(consoleLogPath)!);
        File.WriteAllText(consoleLogPath, "[2026-11-24 00:50:00] Line of an earlier upload\r\n");
        string consoleLogLineWasForwardedPath = Path.Combine(folder.ScriptsPath, "console-log-line-was-forwarded.txt");
        string steamCmdStandIn = folder.WriteScript("steamcmd-stand-in.cmd",
            "@echo [2026-11-24 00:56:51] Uploading content...>>logs\\console_log.txt\r\n"
            + ":wait\r\n"
            + "@if not exist console-log-line-was-forwarded.txt (ping -n 2 127.0.0.1 >nul & goto wait)\r\n"
            + "@echo Line on stdout\r\n"
            + "@echo Line on stderr>&2\r\n");
        ConcurrentQueue<string> lines = new ConcurrentQueue<string>();

        await SteamCmd.RunAppBuildAsync(steamCmdStandIn, "builder_account", "app_100030.vdf", line =>
        {
            lines.Enqueue(line);
            if (line == "Uploading content...")
            {
                File.WriteAllText(consoleLogLineWasForwardedPath, string.Empty);
            }
        }, CancellationToken.None);

        Assert.That(lines, Is.EqualTo(new[] { "Uploading content...", "Line on stderr" }));
    }

    [Test]
    public void RunAppBuildAsync_StopsTheProcessWhenCancelled()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string steamCmdStandIn = folder.WriteScript("steamcmd-stand-in.cmd", "@ping -n 30 127.0.0.1 >nul\r\n");
        using CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        Assert.That(async () => await SteamCmd.RunAppBuildAsync(steamCmdStandIn, "builder_account", "app_100030.vdf",
            _ => { }, cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void RunAppBuildAsync_ReturnsTheExitCodeWhenSteamCmdHadEndedBeforeTheStop()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string steamCmdStandIn = folder.WriteScript("steamcmd-stand-in.cmd",
            "@ping -n 2 127.0.0.1 >nul\r\n@exit /b 3\r\n");
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        ContinuationsQueuedUntilRun continuations = new ContinuationsQueuedUntilRun();
        SynchronizationContext? contextOfTheTest = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(continuations);
        try
        {
            Task<int> run = SteamCmd.RunAppBuildAsync(steamCmdStandIn, "builder_account", "app_100030.vdf",
                _ => { }, cancellation.Token);
            Assert.That(SpinWait.SpinUntil(() => continuations.Count > 0, TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(run.IsCompleted, Is.False);

            cancellation.Cancel();
            Assert.That(SpinWait.SpinUntil(() =>
            {
                continuations.RunQueued();

                return run.IsCompleted;
            }, TimeSpan.FromSeconds(10)), Is.True);

            Assert.That(run.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(run.Result, Is.EqualTo(3));
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(contextOfTheTest);
        }
    }

    [Test]
    public void RunAppBuildAsync_StopsAChildProcessThatOutlivesTheProcessThatStartedIt()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string steamCmdStandIn = folder.WriteScript("steamcmd-stand-in.cmd",
            "@ping -n 2 127.0.0.1 >nul\r\n"
            + "@start \"\" /b ping -n 30 127.0.0.1 >nul\r\n"
            + "@echo child started\r\n"
            + "@exit /b 0\r\n");
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        using ManualResetEventSlim childStarted = new ManualResetEventSlim();

        Task<int> run = SteamCmd.RunAppBuildAsync(steamCmdStandIn, "builder_account", "app_100030.vdf", line =>
        {
            if (line == "child started")
            {
                childStarted.Set();
            }
        }, cancellation.Token);
        Assert.That(childStarted.Wait(TimeSpan.FromSeconds(10)), Is.True);
        Thread.Sleep(TimeSpan.FromSeconds(1));
        Assert.That(run.IsCompleted, Is.False);
        Stopwatch timeSinceStop = Stopwatch.StartNew();
        cancellation.Cancel();

        Assert.That(async () => await run, Throws.InstanceOf<OperationCanceledException>());
        Assert.That(timeSinceStop.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
    }
}
