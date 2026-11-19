using System.ComponentModel;

namespace DepotUploader;

internal sealed class MainForm : Form
{
    private const int ContentFolderColumn = 1;
    private const int ContentSummaryColumn = 2;

    private readonly string _settingsPath = UploaderSettings.DefaultFilePath;
    private readonly UploaderSettings _settings;
    private readonly List<AppBuildScript> _games = new List<AppBuildScript>();

    private readonly TextBox _contentBuilderPathBox = new TextBox { Dock = DockStyle.Fill };
    private readonly TextBox _steamLoginBox = new TextBox { Dock = DockStyle.Fill };
    private readonly ListBox _gameList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox _gameNameBox = new TextBox { Dock = DockStyle.Fill };
    private readonly TextBox _appIdBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly TextBox _descriptionBox = new TextBox { Dock = DockStyle.Fill };
    private readonly TextBox _liveBranchBox = new TextBox { Dock = DockStyle.Fill };
    private readonly CheckBox _uploadsNothingBox = new CheckBox { Text = "Preview only, upload nothing", AutoSize = true };
    private readonly DataGridView _depotGrid = new DataGridView();
    private readonly Button _browseDepotFolderButton = new Button { Text = "Choose content folder...", AutoSize = true };
    private readonly Button _saveButton = new Button { Text = "Save", AutoSize = true };
    private readonly Button _uploadButton = new Button { Text = "Upload", AutoSize = true };
    private readonly Button _stopButton = new Button { Text = "Stop", AutoSize = true, Enabled = false };
    private readonly TextBox _logBox = new TextBox
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Font = new Font(FontFamily.GenericMonospace, 9f),
    };

    private ContentBuilderFolder? _contentBuilder;
    private AppBuildScript? _shownGame;
    private CancellationTokenSource? _uploadCancellation;
    private bool _ignoreGameSelectionChange;

    public MainForm()
    {
        _settings = UploaderSettings.Load(_settingsPath);

        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Depot Uploader";
        ClientSize = new Size(1100, 760);
        MinimumSize = new Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;

        TableLayoutPanel gamesAndDetails = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        gamesAndDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        gamesAndDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        gamesAndDetails.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        gamesAndDetails.Controls.Add(BuildGameListPanel(), 0, 0);
        gamesAndDetails.Controls.Add(BuildGameDetailsPanel(), 1, 0);

        TableLayoutPanel windowLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        windowLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        windowLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        windowLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        windowLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        windowLayout.Controls.Add(BuildSteamSetupPanel(), 0, 0);
        windowLayout.Controls.Add(gamesAndDetails, 0, 1);
        windowLayout.Controls.Add(_logBox, 0, 2);
        Controls.Add(windowLayout);

        _contentBuilderPathBox.Text = _settings.ContentBuilderPath;
        _steamLoginBox.Text = _settings.SteamLogin;
        _gameList.SelectedIndexChanged += (_, _) => SwitchToSelectedGame();
        FormClosing += (_, _) => StopUploadAndSaveSettings();
        Shown += (_, _) => ReloadGames();
    }

    private Control BuildSteamSetupPanel()
    {
        Button browseButton = new Button { Text = "Browse...", AutoSize = true };
        browseButton.Click += (_, _) => BrowseContentBuilderFolder();
        Button reloadButton = new Button { Text = "Reload", AutoSize = true };
        reloadButton.Click += (_, _) => ReloadGames();
        Button signInButton = new Button { Text = "Sign in...", AutoSize = true };
        signInButton.Click += (_, _) => OpenSignInConsole();

        TableLayoutPanel panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(8),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.Controls.Add(NewLabel("ContentBuilder folder"), 0, 0);
        panel.Controls.Add(_contentBuilderPathBox, 1, 0);
        panel.Controls.Add(browseButton, 2, 0);
        panel.Controls.Add(reloadButton, 3, 0);
        panel.Controls.Add(NewLabel("Steam login"), 0, 1);
        panel.Controls.Add(_steamLoginBox, 1, 1);
        panel.Controls.Add(signInButton, 2, 1);

        return panel;
    }

    private Control BuildGameListPanel()
    {
        Button addGameButton = new Button { Text = "Add game...", Dock = DockStyle.Bottom, AutoSize = true };
        addGameButton.Click += (_, _) => AddGame();

        Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        panel.Controls.Add(_gameList);
        panel.Controls.Add(addGameButton);

        return panel;
    }

    private Control BuildGameDetailsPanel()
    {
        _depotGrid.Dock = DockStyle.Fill;
        _depotGrid.AllowUserToAddRows = false;
        _depotGrid.AllowUserToDeleteRows = false;
        _depotGrid.RowHeadersVisible = false;
        _depotGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _depotGrid.MultiSelect = false;
        _depotGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Depot", ReadOnly = true, Width = 90 });
        _depotGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Content folder",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });
        _depotGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Contents",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
        });
        _depotGrid.CellEndEdit += (_, cell) => ShowContentSummary(_depotGrid.Rows[cell.RowIndex]);

        _browseDepotFolderButton.Click += (_, _) => BrowseDepotContentFolder();
        _saveButton.Click += (_, _) => SaveShownGame();
        _uploadButton.Click += async (_, _) => await UploadShownGameAsync();
        _stopButton.Click += (_, _) => _uploadCancellation?.Cancel();

        FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        buttons.Controls.Add(_browseDepotFolderButton);
        buttons.Controls.Add(_saveButton);
        buttons.Controls.Add(_uploadButton);
        buttons.Controls.Add(_stopButton);

        TableLayoutPanel panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(8),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 5; row++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(NewLabel("Name"), 0, 0);
        panel.Controls.Add(_gameNameBox, 1, 0);
        panel.Controls.Add(NewLabel("App ID"), 0, 1);
        panel.Controls.Add(_appIdBox, 1, 1);
        panel.Controls.Add(NewLabel("Build description"), 0, 2);
        panel.Controls.Add(_descriptionBox, 1, 2);
        panel.Controls.Add(NewLabel("Set live on branch"), 0, 3);
        panel.Controls.Add(_liveBranchBox, 1, 3);
        panel.Controls.Add(_uploadsNothingBox, 1, 4);
        panel.Controls.Add(_depotGrid, 0, 5);
        panel.SetColumnSpan(_depotGrid, 2);
        panel.Controls.Add(buttons, 0, 6);
        panel.SetColumnSpan(buttons, 2);

        return panel;
    }

    private static Label NewLabel(string text)
    {
        return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };
    }

    private void BrowseContentBuilderFolder()
    {
        using FolderBrowserDialog dialog = new FolderBrowserDialog
        {
            Description = "Choose the ContentBuilder folder of the Steamworks SDK (sdk\\tools\\ContentBuilder)",
            UseDescriptionForTitle = true,
            InitialDirectory = _contentBuilderPathBox.Text,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _contentBuilderPathBox.Text = dialog.SelectedPath;
            ReloadGames();
        }
    }

    private void ReloadGames()
    {
        string? selectedAppId = _shownGame?.AppId;
        _shownGame = null;
        _games.Clear();
        _gameList.Items.Clear();
        _contentBuilder = null;

        string folderPath = _contentBuilderPathBox.Text.Trim();
        if (folderPath.Length == 0)
        {
            ShowGame(null);
            AppendLog("Choose the ContentBuilder folder of the Steamworks SDK to list its games.");

            return;
        }

        ContentBuilderFolder contentBuilder = new ContentBuilderFolder(folderPath);
        if (!File.Exists(contentBuilder.SteamCmdPath))
        {
            ShowGame(null);
            AppendLog($"steamcmd.exe was not found at {contentBuilder.SteamCmdPath}.");

            return;
        }

        _contentBuilder = contentBuilder;
        _games.AddRange(contentBuilder.LoadAppBuildScripts(out List<string> scriptLoadErrors));
        foreach (AppBuildScript game in _games)
        {
            _gameList.Items.Add(GameListText(game));
        }

        AppendLog($"Found {_games.Count} game(s) in {contentBuilder.ScriptsFolder}.");
        foreach (string scriptLoadError in scriptLoadErrors)
        {
            AppendLog($"Could not read {scriptLoadError}");
        }

        int indexToSelect = _games.FindIndex(game => game.AppId == selectedAppId);
        if (indexToSelect < 0 && _games.Count > 0)
        {
            indexToSelect = 0;
        }

        if (indexToSelect < 0)
        {
            ShowGame(null);
        }
        else
        {
            _gameList.SelectedIndex = indexToSelect;
        }
    }

    private string GameListText(AppBuildScript game)
    {
        return $"{_settings.GameName(game.AppId)} ({game.AppId})";
    }

    private void SwitchToSelectedGame()
    {
        if (_ignoreGameSelectionChange)
        {
            return;
        }

        AppBuildScript? selectedGame = _gameList.SelectedIndex < 0 ? null : _games[_gameList.SelectedIndex];
        if (ReferenceEquals(selectedGame, _shownGame))
        {
            return;
        }

        if (_shownGame is not null && HasUnsavedEdits(_shownGame))
        {
            DialogResult answer = MessageBox.Show(this,
                $"Save the changes to {_settings.GameName(_shownGame.AppId)}?", Text,
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel || (answer == DialogResult.Yes && !SaveShownGame()))
            {
                _ignoreGameSelectionChange = true;
                _gameList.SelectedIndex = _games.IndexOf(_shownGame);
                _ignoreGameSelectionChange = false;

                return;
            }
        }

        ShowGame(selectedGame);
    }

    private void ShowGame(AppBuildScript? game)
    {
        _shownGame = game;
        bool hasGame = game is not null;
        _gameNameBox.Enabled = hasGame;
        _descriptionBox.Enabled = hasGame;
        _liveBranchBox.Enabled = hasGame;
        _uploadsNothingBox.Enabled = hasGame;
        _depotGrid.Enabled = hasGame;
        _browseDepotFolderButton.Enabled = hasGame;
        _saveButton.Enabled = hasGame;
        _uploadButton.Enabled = hasGame && _uploadCancellation is null;
        _depotGrid.Rows.Clear();
        if (game is null)
        {
            _gameNameBox.Text = string.Empty;
            _appIdBox.Text = string.Empty;
            _descriptionBox.Text = string.Empty;
            _liveBranchBox.Text = string.Empty;
            _uploadsNothingBox.Checked = false;

            return;
        }

        _gameNameBox.Text = _settings.GameName(game.AppId);
        _appIdBox.Text = game.AppId;
        _descriptionBox.Text = game.Description;
        _liveBranchBox.Text = game.LiveBranch;
        _uploadsNothingBox.Checked = game.UploadsNothing;
        foreach (DepotBuildScript depot in game.Depots)
        {
            DataGridViewRow row = _depotGrid.Rows[_depotGrid.Rows.Add(depot.DepotId, depot.ContentFolder, string.Empty)];
            row.Tag = depot;
            ShowContentSummary(row);
        }
    }

    private static DepotBuildScript DepotInRow(DataGridViewRow row)
    {
        return (DepotBuildScript)row.Tag!;
    }

    private static string ContentFolderInRow(DataGridViewRow row)
    {
        return (row.Cells[ContentFolderColumn].Value as string ?? string.Empty).Trim();
    }

    private async void ShowContentSummary(DataGridViewRow row)
    {
        string folder = ContentFolderInRow(row);
        string absoluteFolder = _shownGame!.AbsoluteContentFolder(DepotInRow(row), folder);
        row.Cells[ContentSummaryColumn].Value = "Counting files...";
        string summary;
        try
        {
            summary = await Task.Run(() => ContentFolderSummary.Describe(absoluteFolder));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            summary = exception.Message;
        }

        if (IsDisposed || row.DataGridView is null || ContentFolderInRow(row) != folder)
        {
            return;
        }

        row.Cells[ContentSummaryColumn].Value = summary;
    }

    private void BrowseDepotContentFolder()
    {
        DataGridViewRow? row = _depotGrid.CurrentRow;
        if (row is null)
        {
            return;
        }

        using FolderBrowserDialog dialog = new FolderBrowserDialog
        {
            Description = $"Choose the build folder to upload to depot {DepotInRow(row).DepotId}",
            UseDescriptionForTitle = true,
            InitialDirectory = _shownGame!.AbsoluteContentFolder(DepotInRow(row), ContentFolderInRow(row)),
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            row.Cells[ContentFolderColumn].Value = dialog.SelectedPath;
            ShowContentSummary(row);
        }
    }

    private bool HasUnsavedEdits(AppBuildScript game)
    {
        if (_gameNameBox.Text.Trim() != _settings.GameName(game.AppId)
            || _descriptionBox.Text != game.Description
            || _liveBranchBox.Text.Trim() != game.LiveBranch
            || _uploadsNothingBox.Checked != game.UploadsNothing)
        {
            return true;
        }

        foreach (DataGridViewRow row in _depotGrid.Rows)
        {
            if (ContentFolderInRow(row) != DepotInRow(row).ContentFolder)
            {
                return true;
            }
        }

        return false;
    }

    private bool SaveShownGame()
    {
        if (_shownGame is null)
        {
            return false;
        }

        _depotGrid.EndEdit();
        AppBuildScript game = _shownGame;
        string previousDescription = game.Description;
        string previousLiveBranch = game.LiveBranch;
        bool previousUploadsNothing = game.UploadsNothing;
        List<string> previousContentFolders = game.Depots.Select(depot => depot.ContentFolder).ToList();

        game.Description = _descriptionBox.Text;
        game.LiveBranch = _liveBranchBox.Text.Trim();
        game.UploadsNothing = _uploadsNothingBox.Checked;
        foreach (DataGridViewRow row in _depotGrid.Rows)
        {
            DepotInRow(row).ContentFolder = ContentFolderInRow(row);
        }

        try
        {
            game.Save();
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
        {
            game.Description = previousDescription;
            game.LiveBranch = previousLiveBranch;
            game.UploadsNothing = previousUploadsNothing;
            for (int depotIndex = 0; depotIndex < game.Depots.Count; depotIndex++)
            {
                game.Depots[depotIndex].ContentFolder = previousContentFolders[depotIndex];
            }

            MessageBox.Show(this, $"The build scripts were not saved. {exception.Message}", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);

            return false;
        }

        _settings.GameNamesByAppId[game.AppId] = _gameNameBox.Text.Trim();
        SaveSettings();
        _ignoreGameSelectionChange = true;
        _gameList.Items[_games.IndexOf(game)] = GameListText(game);
        _ignoreGameSelectionChange = false;
        AppendLog($"Saved {Path.GetFileName(game.FilePath)} and {game.Depots.Count} depot script(s).");

        return true;
    }

    private void SaveSettings()
    {
        _settings.ContentBuilderPath = _contentBuilderPathBox.Text.Trim();
        _settings.SteamLogin = _steamLoginBox.Text.Trim();
        try
        {
            _settings.Save(_settingsPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppendLog($"The settings were not saved. {exception.Message}");
        }
    }

    private void AddGame()
    {
        if (_contentBuilder is null)
        {
            MessageBox.Show(this, "Choose the ContentBuilder folder first.", Text, MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return;
        }

        using AddGameForm dialog = new AddGameForm();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _contentBuilder.CreateAppBuildScript(dialog.AppId, dialog.DepotIds);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException
            or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"The game was not added. {exception.Message}", Text, MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        if (dialog.GameName.Length > 0)
        {
            _settings.GameNamesByAppId[dialog.AppId] = dialog.GameName;
        }

        SaveSettings();
        ReloadGames();
        _gameList.SelectedIndex = _games.FindIndex(game => game.AppId == dialog.AppId);
    }

    private bool CanRunSteamCmdOrExplainWhyNot()
    {
        if (_contentBuilder is null || !File.Exists(_contentBuilder.SteamCmdPath))
        {
            MessageBox.Show(this, "Choose a ContentBuilder folder that contains builder\\steamcmd.exe.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            return false;
        }

        if (_steamLoginBox.Text.Trim().Length == 0)
        {
            MessageBox.Show(this, "Enter the Steam login name of your build account.", Text, MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return false;
        }

        return true;
    }

    private void OpenSignInConsole()
    {
        if (!CanRunSteamCmdOrExplainWhyNot())
        {
            return;
        }

        SaveSettings();
        SteamCmd.OpenSignInConsole(_contentBuilder!.SteamCmdPath, _steamLoginBox.Text.Trim());
        AppendLog("Opened steamcmd in its own window. Type your password and Steam Guard code there. "
            + "steamcmd keeps the sign-in for later uploads.");
    }

    private async Task UploadShownGameAsync()
    {
        if (_shownGame is null || !CanRunSteamCmdOrExplainWhyNot() || !SaveShownGame())
        {
            return;
        }

        AppBuildScript game = _shownGame;
        ContentBuilderFolder contentBuilder = _contentBuilder!;
        foreach (DepotBuildScript depot in game.Depots)
        {
            string contentFolder = game.AbsoluteContentFolder(depot);
            if (!Directory.Exists(contentFolder))
            {
                MessageBox.Show(this, contentFolder.Length == 0
                        ? $"Depot {depot.DepotId} has no content folder."
                        : $"Depot {depot.DepotId} has no content folder at \"{contentFolder}\".",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);

                return;
            }
        }

        if (MessageBox.Show(this, UploadConfirmationText(game), Text, MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }

        string scriptCopiesFolder = Path.Combine(Path.GetTempPath(), "DepotUploader", Guid.NewGuid().ToString("N"));
        string appScriptCopyPath;
        try
        {
            appScriptCopyPath = game.WriteCopiesWithAbsolutePaths(scriptCopiesFolder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException)
        {
            DeleteScriptCopies(scriptCopiesFolder);
            MessageBox.Show(this, $"The upload was not started. {exception.Message}", Text, MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        string buildLogPath = contentBuilder.AppBuildLogPath(game.AppId);
        long buildLogLengthBefore = File.Exists(buildLogPath) ? new FileInfo(buildLogPath).Length : 0;
        _uploadCancellation = new CancellationTokenSource();
        _uploadButton.Enabled = false;
        _stopButton.Enabled = true;
        AppendLog($"Starting steamcmd for {_settings.GameName(game.AppId)} ({game.AppId}).");
        try
        {
            int exitCode = await SteamCmd.RunAppBuildAsync(contentBuilder.SteamCmdPath, _steamLoginBox.Text.Trim(),
                appScriptCopyPath, AppendLogFromAnyThread, _uploadCancellation.Token);
            AppendBuildLogWrittenAfter(buildLogPath, buildLogLengthBefore);
            AppendLog(exitCode == 0
                ? "steamcmd finished with exit code 0."
                : $"steamcmd failed with exit code {exitCode}. If it could not sign in, use Sign in and upload again.");
        }
        catch (OperationCanceledException)
        {
            AppendLog("Stopped steamcmd.");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException
            or Win32Exception)
        {
            AppendLog($"steamcmd could not be started. {exception.Message}");
        }
        finally
        {
            _uploadCancellation.Dispose();
            _uploadCancellation = null;
            DeleteScriptCopies(scriptCopiesFolder);
            if (!IsDisposed)
            {
                _stopButton.Enabled = false;
                _uploadButton.Enabled = _shownGame is not null;
            }
        }
    }

    private void DeleteScriptCopies(string scriptCopiesFolder)
    {
        try
        {
            if (Directory.Exists(scriptCopiesFolder))
            {
                Directory.Delete(scriptCopiesFolder, true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppendLog($"The script copies in {scriptCopiesFolder} were not deleted. {exception.Message}");
        }
    }

    private string UploadConfirmationText(AppBuildScript game)
    {
        List<string> lines = new List<string>
        {
            game.UploadsNothing
                ? $"Run a preview build of {_settings.GameName(game.AppId)} ({game.AppId})? Nothing is uploaded."
                : $"Upload {_settings.GameName(game.AppId)} ({game.AppId}) to Steam?",
            string.Empty,
            $"Description: {game.Description}",
            game.LiveBranch.Length == 0 ? "Set live: no" : $"Set live on branch: {game.LiveBranch}",
        };
        foreach (DataGridViewRow row in _depotGrid.Rows)
        {
            lines.Add($"Depot {DepotInRow(row).DepotId}: {game.AbsoluteContentFolder(DepotInRow(row))}");
            lines.Add($"    {row.Cells[ContentSummaryColumn].Value}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void AppendBuildLogWrittenAfter(string buildLogPath, long lengthBefore)
    {
        if (!File.Exists(buildLogPath))
        {
            return;
        }

        try
        {
            using FileStream stream = new FileStream(buildLogPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite);
            if (stream.Length < lengthBefore)
            {
                lengthBefore = 0;
            }

            stream.Seek(lengthBefore, SeekOrigin.Begin);
            using StreamReader reader = new StreamReader(stream);
            string newText = reader.ReadToEnd().Trim();
            if (newText.Length > 0)
            {
                AppendLog($"{Path.GetFileName(buildLogPath)}:");
                AppendLog(newText.ReplaceLineEndings());
            }
        }
        catch (IOException exception)
        {
            AppendLog($"{Path.GetFileName(buildLogPath)} could not be read. {exception.Message}");
        }
    }

    private void AppendLogFromAnyThread(string line)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(() => AppendLog(line));
        }
        catch (InvalidOperationException)
        {
            return;
        }
    }

    private void AppendLog(string text)
    {
        if (IsDisposed)
        {
            return;
        }

        _logBox.AppendText(text + Environment.NewLine);
    }

    private void StopUploadAndSaveSettings()
    {
        _uploadCancellation?.Cancel();
        SaveSettings();
    }
}
