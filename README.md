# Depot Uploader

A Windows tool that uploads game builds to Steam with `steamcmd`. It lists every game that has an app build script in the Steamworks SDK's `ContentBuilder\scripts` folder, and you pick the one to upload. Games already set up with Valve's SteamPipe GUI appear in the list.

## Requirements

- Windows
- .NET 8 SDK to build, .NET 8 Desktop Runtime to run
- The Steamworks SDK with `sdk\tools\ContentBuilder\builder\steamcmd.exe`

## Build and run

```
dotnet publish src/DepotUploader -c Release -o publish
publish\DepotUploader.exe
```

Run the tests with `dotnet test`.

## Use

1. Choose the `ContentBuilder` folder and enter your Steam login name.
2. Click **Sign in...** once. steamcmd opens in its own window and asks for your password and Steam Guard code there. steamcmd saves the sign-in for later uploads.
3. Select a game, set the build description and the content folder of each depot, then click **Upload**.

The game list shows every `app_<appid>.vdf` in `ContentBuilder\scripts`. These are the scripts SteamPipe GUI writes, and both tools can edit them. When an app script can't be read, the log names it and the other games still load. **Add game...** writes a new `app_<appid>.vdf` and one `depot_<depotid>.vdf` per depot. Each depot uploads its whole content folder except `*.pdb` files.

**Save** writes only the scripts you edited, and it writes all of them or none. When a script changed on disk after the tool loaded it, **Save** writes nothing and names the script. Click **Reload** to read the scripts again. **Reload** discards the edits in the window.

**Upload** saves the scripts, asks you to confirm, then runs steamcmd on copies of the scripts in a temporary folder. The copies use absolute paths. Editing a script while steamcmd runs doesn't change that upload.

| Field | Script key | Effect |
|---|---|---|
| Build description | `desc` | The label of the build in Steamworks |
| Set live on branch | `setlive` | The branch that gets the build after the upload. Leave it empty to set the build live in Steamworks yourself. |
| Preview only, upload nothing | `preview` | steamcmd builds the depot manifests and uploads nothing |
| Content folder | `contentroot` in the depot script | The build folder that goes into the depot. Leave it empty to use `contentroot` of the app script. |

A relative content folder is relative to the folder of its script. The Contents column shows the file count, total size and newest file time of each content folder. The confirmation before an upload shows the same numbers and the absolute path of each folder.

After steamcmd exits, the log shows its exit code and the lines it added to `ContentBuilder\output\app_build_<appid>.log`.

## Limits

- The tool never asks for, stores or passes a password. steamcmd needs a saved sign-in to upload. When an upload stops at the login, click **Sign in...** and upload again.
- Depots written inside the app script are not supported. Each depot needs its own script file.
- A depot needs a content folder in its own script or in the app script. The tool doesn't upload a game when one of its depots has neither.
- Values can't contain a double quote.
- The game names, the ContentBuilder path and the Steam login name are saved in `%APPDATA%\DepotUploader\settings.json`. The names are labels for the list and are not written to the scripts.
- Comments in a script are removed when the tool saves it.

## License

MIT, see [LICENSE](LICENSE).

Depot Uploader is not affiliated with Valve. This repository contains no Valve software. You need your own copy of the Steamworks SDK. Steam, Steamworks and SteamPipe are trademarks of Valve Corporation.
