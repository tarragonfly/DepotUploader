namespace DepotUploader;

internal sealed class SteamCmdNotStoppedException : Exception
{
    public SteamCmdNotStoppedException()
        : base("steamcmd or one of its child processes may still be running and uploading. "
            + "End steamcmd.exe in Task Manager.")
    {
    }
}
