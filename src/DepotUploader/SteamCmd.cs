using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DepotUploader;

internal static class SteamCmd
{
    private static readonly TimeSpan TimeAllowedToStop = TimeSpan.FromSeconds(10);

    public static async Task<int> RunAppBuildAsync(string steamCmdPath, string steamLogin, string appScriptPath,
        Action<string> onOutputLine, CancellationToken cancellation)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo(steamCmdPath)
        {
            Arguments = QuotedArguments("+login", steamLogin, "+run_app_build", appScriptPath, "+quit"),
            WorkingDirectory = Path.GetDirectoryName(steamCmdPath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using Process process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, line) => ForwardLine(line.Data, onOutputLine);
        process.ErrorDataReceived += (_, line) => ForwardLine(line.Data, onOutputLine);
        process.Start();
        IntPtr jobOfSteamCmdAndItsChildren = CreateJobObject(IntPtr.Zero, null);
        try
        {
            bool steamCmdIsInJob = jobOfSteamCmdAndItsChildren != IntPtr.Zero
                && AssignProcessToJobObject(jobOfSteamCmdAndItsChildren, process.Handle);
            process.StandardInput.Close();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Task exitAndEndOfOutput = Task.Run(() => process.WaitForExit());
            TaskCompletionSource<bool> endedEveryProcessOfJob = new TaskCompletionSource<bool>();
            using (cancellation.Register(() =>
                   {
                       bool terminatedJob = steamCmdIsInJob && TerminateJobObject(jobOfSteamCmdAndItsChildren, 1);
                       if (!terminatedJob)
                       {
                           KillIfRunning(process);
                       }

                       endedEveryProcessOfJob.TrySetResult(terminatedJob);
                   }))
            {
                await Task.WhenAny(exitAndEndOfOutput, endedEveryProcessOfJob.Task);
                if (!cancellation.IsCancellationRequested)
                {
                    await exitAndEndOfOutput;

                    return process.ExitCode;
                }
            }

            bool sawExitAndEndOfOutput =
                await Task.WhenAny(exitAndEndOfOutput, Task.Delay(TimeAllowedToStop)) == exitAndEndOfOutput;
            if (!await endedEveryProcessOfJob.Task || !sawExitAndEndOfOutput)
            {
                throw new SteamCmdNotStoppedException();
            }

            throw new OperationCanceledException(cancellation);
        }
        finally
        {
            if (jobOfSteamCmdAndItsChildren != IntPtr.Zero)
            {
                CloseHandle(jobOfSteamCmdAndItsChildren);
            }
        }
    }

    public static void OpenSignInConsole(string steamCmdPath, string steamLogin)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo(steamCmdPath)
        {
            Arguments = QuotedArguments("+login", steamLogin, "+quit"),
            WorkingDirectory = Path.GetDirectoryName(steamCmdPath)!,
            UseShellExecute = true,
        };
        Process.Start(startInfo)?.Dispose();
    }

    internal static string QuotedArguments(params string[] arguments)
    {
        StringBuilder commandLine = new StringBuilder();
        foreach (string argument in arguments)
        {
            if (commandLine.Length > 0)
            {
                commandLine.Append(' ');
            }

            if (argument.Length > 0 && !argument.Any(character => char.IsWhiteSpace(character) || character == '"'))
            {
                commandLine.Append(argument);
                continue;
            }

            commandLine.Append('"');
            int backslashesBeforeCharacter = 0;
            foreach (char character in argument)
            {
                if (character == '\\')
                {
                    backslashesBeforeCharacter++;
                    continue;
                }

                if (character == '"')
                {
                    commandLine.Append('\\', backslashesBeforeCharacter * 2 + 1);
                }
                else
                {
                    commandLine.Append('\\', backslashesBeforeCharacter);
                }

                commandLine.Append(character);
                backslashesBeforeCharacter = 0;
            }

            commandLine.Append('\\', backslashesBeforeCharacter * 2).Append('"');
        }

        return commandLine.ToString();
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            return;
        }
    }

    private static void ForwardLine(string? line, Action<string> onOutputLine)
    {
        if (line is not null)
        {
            onOutputLine(line);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr securityAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
