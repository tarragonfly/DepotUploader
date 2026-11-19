using System.Collections.Concurrent;

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

    [Test]
    public void RunAppBuildAsync_StopsTheProcessWhenCancelled()
    {
        using TemporaryContentBuilderFolder folder = new TemporaryContentBuilderFolder();
        string steamCmdStandIn = folder.WriteScript("steamcmd-stand-in.cmd", "@ping -n 30 127.0.0.1 >nul\r\n");
        using CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        Assert.That(async () => await SteamCmd.RunAppBuildAsync(steamCmdStandIn, "builder_account", "app_100030.vdf",
            _ => { }, cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
    }
}
