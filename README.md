# Depot Uploader

A Windows tool that uploads game builds to Steam with `steamcmd` and keeps all your games in one window.

![Depot Uploader window with three games in the list and the depots of the selected game](docs/screenshot.png)

Valve's SteamPipe GUI handles one game per window. Depot Uploader lists every game that has an `app_<appid>.vdf` script in the Steamworks SDK's `ContentBuilder\scripts` folder. Games set up with SteamPipe GUI appear in the list, and both tools can edit the same scripts.

## Download

Download `DepotUploader-<version>-win-x64.exe` from the [latest release](https://github.com/tarragonfly/DepotUploader/releases/latest). It's a single 129 KB exe.

Requirements:

- Windows 10 version 1903 or later, or Windows 11. The exe is framework-dependent and requires .NET Framework 4.8, which these versions include.
- The Steamworks SDK, for `sdk\tools\ContentBuilder`.

The exe is not code-signed. Windows SmartScreen may show a warning on first run.

## Use

1. At the bottom of the window, choose the `ContentBuilder` folder and enter your Steam login name.
2. Click **Sign in** once. steamcmd opens in its own window and asks for your password and Steam Guard code there. steamcmd saves the sign-in for later uploads.
3. Select a game, set the build description and the content folder of each depot, then click **Upload**.

The box next to the funnel icon filters the game list: it shows only the games whose name or app ID contains the text you type. The box next to the sort icon sets the order of the list:

| Choice | Order |
|---|---|
| Name A-Z | The game name, ignoring case |
| Last build first | The write time of `app_build_<appid>.log` in the `buildoutput` folder of the app script, newest first. steamcmd writes this log on every build, including preview builds and builds started from SteamPipe GUI or the command line. |
| Recently added first | The creation time of `app_<appid>.vdf`, newest first |

**Add game** writes a new `app_<appid>.vdf` and one `depot_<depotid>.vdf` per depot. **Add depot** adds a depot to the selected game. In a depot row, the folder button opens a folder picker for the content folder, and the trash button removes the depot from the game and keeps its `depot_<depotid>.vdf`. The row shows a warning icon when its content folder is not set, not found, empty or unreadable. The game list shows the same icon next to a game that has such a depot. The list icons are updated on **Reload** and when you edit a game.

**Remove game** moves the selected game's `app_<appid>.vdf` and its depot scripts to `scripts\removed`. A depot script is moved only when it is directly in `scripts` and no other `.vdf` file directly in `scripts` refers to it. No depot script is moved when another `.vdf` file in `scripts` can't be parsed. To list the game again, move the files back and click **Reload**.

| Field | Script key | Effect |
|---|---|---|
| Build description | `desc` | The label of the build in Steamworks |
| Set live on branch | `setlive` | The branch that gets the build after the upload. Leave it empty to set the build live in Steamworks yourself. |
| Preview only, upload nothing | `preview` | steamcmd builds the depot manifests and uploads nothing. The **Upload** button reads **Preview** while it is ticked. |
| Content folder | `contentroot` in the depot script | The build folder that goes into the depot. Leave it empty to use `contentroot` of the app script. |

There is no Save button. An edit is saved when you leave its field. **Upload** shows the file count, total size and absolute path of each content folder and asks you to confirm. The log shows steamcmd's output while it runs, then its exit code and the app build log. A line next to **Stop** shows whether the last upload or preview build finished, failed or was stopped, and at what time.

## Limits

- The tool never asks for, stores or passes a password. When an upload stops at the login, click **Sign in** and upload again.
- Each depot needs its own script file. Depots written inside the app script are not supported.
- Values can't contain a double quote.
- Comments in a script are removed when the tool saves it.
- A save fails when a script changed on disk after the tool loaded it. Click **Reload** to read the scripts again. **Reload** discards unsaved edits.
- When a failed save can't restore a file, the error names the `.previous` file that holds it. Rename that file to the original name.
- Game names, the sort order, the ContentBuilder path and the Steam login name are saved in `%APPDATA%\DepotUploader\settings.json`. With two windows open, the last write wins.

## Build from source

Requires the .NET 10 SDK.

```
dotnet publish src/DepotUploader -c Release -o publish
```

## License

MIT, see [LICENSE](LICENSE).

Depot Uploader is not affiliated with Valve. This repository contains no Valve software. You need your own copy of the Steamworks SDK. Steam, Steamworks and SteamPipe are trademarks of Valve Corporation.
