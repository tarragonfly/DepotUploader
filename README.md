# Depot Uploader

A Windows tool that uploads game builds to Steam with `steamcmd` and keeps all your games in one window.

Depot Uploader was made because Valve's SteamPipe GUI doesn't support multiple games in the same window. Depot Uploader lists every game that has an app build script in the Steamworks SDK's `ContentBuilder\scripts` folder, and you pick the one to upload. Games already set up with SteamPipe GUI appear in the list.

## Download

Download `DepotUploader-<version>-win-x64.exe` from the [latest release](https://github.com/tarragonfly/DepotUploader/releases/latest). It's a framework-dependent single exe that requires .NET Framework 4.8, a Windows component since Windows 10 version 1903.

The exe is not code-signed. Windows SmartScreen may show a warning on first run.

## Requirements

- Windows 10 version 1903 or later, or Windows 11. Earlier Windows 10 versions need .NET Framework 4.8 installed.
- The Steamworks SDK with `sdk\tools\ContentBuilder\builder\steamcmd.exe`
- .NET 10 SDK to build from source

## Build and run

```
dotnet publish src/DepotUploader -c Release -o publish
publish\DepotUploader.exe
```

This is the release build: one exe of about 64 KB that targets `net48`, with no other files. Run the tests with `dotnet test`.

## Use

1. Choose the `ContentBuilder` folder and enter your Steam login name.
2. Click **Sign in...** once. steamcmd opens in its own window and asks for your password and Steam Guard code there. steamcmd saves the sign-in for later uploads.
3. Select a game, set the build description and the content folder of each depot, then click **Upload**.

The game list shows every `app_<appid>.vdf` in `ContentBuilder\scripts`. These are the scripts SteamPipe GUI writes, and both tools can edit them. When an app script can't be read, the log names it and the other games still load. **Add game...** writes a new `app_<appid>.vdf` and one `depot_<depotid>.vdf` per depot. Each depot uploads its whole content folder except `*.pdb` files.

**Add depot...** adds a depot to the selected game. It writes `depot_<depotid>.vdf` next to the app script and adds the depot to the `depots` block of the app script. When `depot_<depotid>.vdf` already exists, the tool uses that file and doesn't write it. **Remove** in a depot row removes that depot from the `depots` block. It keeps `depot_<depotid>.vdf`, and adding the depot again uses that file with its content folder. Both buttons write the scripts immediately, without **Save**. Your other unsaved edits stay in the window and are not written.

**Add depot...** only uses `depot_<depotid>.vdf` next to the app script. A depot script with another name or in another folder still loads and uploads. After **Remove** you have to add it back to the `depots` block by hand, and the **Remove** confirmation shows its path.

**Save** writes only the scripts you edited, and it writes all of them or none. When a script changed on disk after the tool loaded it, **Save** writes nothing and names the script. Click **Reload** to read the scripts again. **Reload** discards the edits in the window.

**Upload** saves the scripts, asks you to confirm, then runs steamcmd on copies of the scripts in a temporary folder. The copies use absolute paths. Editing a script while steamcmd runs doesn't change that upload.

| Field | Script key | Effect |
|---|---|---|
| Build description | `desc` | The label of the build in Steamworks |
| Set live on branch | `setlive` | The branch that gets the build after the upload. Leave it empty to set the build live in Steamworks yourself. |
| Preview only, upload nothing | `preview` | steamcmd builds the depot manifests and uploads nothing |
| Content folder | `contentroot` in the depot script | The build folder that goes into the depot. Leave it empty to use `contentroot` of the app script. |

Type the content folder into its cell or pick it with **Browse...** in the same row. A relative content folder is relative to the folder of its script. The Contents column shows the file count, total size and newest file time of each content folder. The confirmation before an upload shows the same numbers and the absolute path of each folder.

After steamcmd exits, the log shows its exit code and the lines it added to `ContentBuilder\output\app_build_<appid>.log`.

## Limits

- The tool never asks for, stores or passes a password. steamcmd needs a saved sign-in to upload. When an upload stops at the login, click **Sign in...** and upload again.
- Depots written inside the app script are not supported. Each depot needs its own script file.
- A depot needs a content folder in its own script or in the app script. The tool doesn't upload a game when one of its depots has neither.
- Values can't contain a double quote.
- The game names, the ContentBuilder path and the Steam login name are saved in `%APPDATA%\DepotUploader\settings.json`. The names are labels for the list and are not written to the scripts. Each window keeps its own copy of the settings and writes the whole file on each save. With two windows open, the last write wins. Edits made to the file while the tool runs are overwritten.
- When `settings.json` can't be read or isn't valid JSON, the tool starts with empty settings and doesn't write the file. The log names the error. Fix or delete the file, then start the tool again.
- A save writes the new text to `<file>.<id>.new` and replaces the file with it. The replaced file is kept as `<file>.<id>.previous` until the save is done. When a save fails and the tool can't put a file back, the error names the `.previous` file that holds it. Rename that file to the original name.
- When `settings.json` is missing and `settings.json.previous` or `settings.json.<id>.previous` exists, the tool starts with empty settings and doesn't save them. Rename the backup to `settings.json`, then start the tool again.
- Ending the process during **Save** can leave `.new` and `.previous` files, and some scripts with the new text and others with the old text.
- Comments in a script are removed when the tool saves it.

## License

MIT, see [LICENSE](LICENSE).

Depot Uploader is not affiliated with Valve. This repository contains no Valve software. You need your own copy of the Steamworks SDK. Steam, Steamworks and SteamPipe are trademarks of Valve Corporation.
