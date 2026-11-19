using System.Diagnostics;

namespace DepotUploader;

internal static class SteamCmd
{
    public static async Task<int> RunAppBuildAsync(string steamCmdPath, string steamLogin, string appScriptPath,
        Action<string> onOutputLine, CancellationToken cancellation)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo(steamCmdPath)
        {
            WorkingDirectory = Path.GetDirectoryName(steamCmdPath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("+login");
        startInfo.ArgumentList.Add(steamLogin);
        startInfo.ArgumentList.Add("+run_app_build");
        startInfo.ArgumentList.Add(appScriptPath);
        startInfo.ArgumentList.Add("+quit");

        using Process process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, line) => ForwardLine(line.Data, onOutputLine);
        process.ErrorDataReceived += (_, line) => ForwardLine(line.Data, onOutputLine);
        process.Start();
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(cancellation);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        return process.ExitCode;
    }

    public static void OpenSignInConsole(string steamCmdPath, string steamLogin)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo(steamCmdPath)
        {
            WorkingDirectory = Path.GetDirectoryName(steamCmdPath)!,
            UseShellExecute = true,
        };
        startInfo.ArgumentList.Add("+login");
        startInfo.ArgumentList.Add(steamLogin);
        startInfo.ArgumentList.Add("+quit");
        Process.Start(startInfo)?.Dispose();
    }

    private static void ForwardLine(string? line, Action<string> onOutputLine)
    {
        if (line is not null)
        {
            onOutputLine(line);
        }
    }
}
