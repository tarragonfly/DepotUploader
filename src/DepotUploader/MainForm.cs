using System.ComponentModel;
using System.Diagnostics;

namespace DepotUploader;

internal sealed class MainForm : Form
{
    private const int ContentFolderColumn = 1;
    private const int BrowseContentFolderColumn = 2;
    private const int ContentSummaryColumn = 3;
    private const int RemoveDepotColumn = 4;

    private readonly string _settingsPath = UploaderSettings.DefaultFilePath;
    private readonly UploaderSettings _settings;
    private readonly List<AppBuildScript> _games = new List<AppBuildScript>();

    private readonly TextBox _contentBuilderPathBox = new TextBox { Dock = DockStyle.Fill };
    private readonly TextBox _steamLoginBox = new TextBox { Dock = DockStyle.Fill };
    private readonly TextBox _gameFilterBox = new TextBox { Dock = DockStyle.Fill };
    private readonly ComboBox _gameSortOrderBox = new ComboBox
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };
    private readonly ListBox _gameList = new ListBox
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        FormattingEnabled = true,
    };
    private readonly Button _removeGameButton = new Button
    {
        Text = "Remove game...",
        Dock = DockStyle.Bottom,
        AutoSize = true,
    };
    private readonly TextBox _gameNameBox = new TextBox { Dock = DockStyle.Fill };
    private readonly TextBox _appIdBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly TextBox _descriptionBox = new TextBox { Dock = DockStyle.Fill };
    private readonly TextBox _liveBranchBox = new TextBox { Dock = DockStyle.Fill };
    private readonly CheckBox _uploadsNothingBox = new CheckBox { Text = "Preview only, upload nothing", AutoSize = true };
    private readonly DataGridView _depotGrid = new DataGridView();
    private readonly Button _addDepotButton = new Button { Text = "Add depot...", AutoSize = true };
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
    private Process? _signInConsole;
    private bool _ignoreGameSelectionChange;

    public MainForm()
    {
        _settings = UploaderSettings.Load(_settingsPath, out string? settingsLoadError);

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Depot Uploader";
        Icon = new Icon(typeof(MainForm).Assembly.GetManifestResourceStream("DepotUploader.ico")!);
        Font = SystemFonts.MessageBoxFont;
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
        windowLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        windowLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        windowLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        windowLayout.Controls.Add(gamesAndDetails, 0, 0);
        windowLayout.Controls.Add(_logBox, 0, 1);
        windowLayout.Controls.Add(BuildSteamSetupPanel(), 0, 2);
        Controls.Add(windowLayout);
        ResumeLayout();
        Size workingAreaSize = Screen.FromPoint(MousePosition).WorkingArea.Size;
        Size = new Size(Math.Min(Width, workingAreaSize.Width), Math.Min(Height, workingAreaSize.Height));

        _contentBuilderPathBox.Text = _settings.ContentBuilderPath;
        _steamLoginBox.Text = _settings.SteamLogin;
        _gameSortOrderBox.SelectedIndex = (int)_settings.GameSortOrder;
        if (settingsLoadError is not null)
        {
            AppendLog(settingsLoadError);
        }

        _gameFilterBox.TextChanged += (_, _) => ListGames(_shownGame);
        _gameSortOrderBox.SelectedIndexChanged += (_, _) => SaveSortOrderAndListGames();
        _gameList.Format += (_, item) => item.Value = GameListText((AppBuildScript)item.ListItem);
        _gameList.SelectedIndexChanged += (_, _) => SwitchToSelectedGame();
        FormClosing += (_, closing) => SaveEditsAndSettingsOrCancelClosing(closing);
        Shown += (_, _) => ReloadGames();
    }

    private Control BuildSteamSetupPanel()
    {
        Button browseButton = new Button { Text = "Browse...", AutoSize = true };
        browseButton.Click += (_, _) => BrowseContentBuilderFolder();
        Button reloadButton = new Button { Text = "Reload", AutoSize = true, CausesValidation = false };
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
            CausesValidation = false,
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
        _removeGameButton.Click += (_, _) => RemoveShownGame();
        _gameSortOrderBox.Items.AddRange(new object[] { "Name A-Z", "Last build first", "Recently added first" });

        TableLayoutPanel filterAndSortOrder = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 0, 0, 4),
        };
        filterAndSortOrder.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterAndSortOrder.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterAndSortOrder.Controls.Add(NewLabel("Filter"), 0, 0);
        filterAndSortOrder.Controls.Add(_gameFilterBox, 1, 0);
        filterAndSortOrder.Controls.Add(NewLabel("Sort by"), 0, 1);
        filterAndSortOrder.Controls.Add(_gameSortOrderBox, 1, 1);

        Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        panel.Controls.Add(_gameList);
        panel.Controls.Add(filterAndSortOrder);
        panel.Controls.Add(addGameButton);
        panel.Controls.Add(_removeGameButton);
        filterAndSortOrder.TabIndex = 0;
        _gameList.TabIndex = 1;
        addGameButton.TabIndex = 2;
        _removeGameButton.TabIndex = 3;

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
        _depotGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _depotGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Depot",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
        });
        _depotGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Content folder",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });
        _depotGrid.Columns.Add(NewDepotRowButtonColumn("Browse..."));
        _depotGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Contents",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
        });
        _depotGrid.Columns.Add(NewDepotRowButtonColumn("X"));
        _depotGrid.CellEndEdit += (_, cell) =>
        {
            if (_shownGame is not null)
            {
                ShowContentSummary(_depotGrid.Rows[cell.RowIndex]);
                SaveShownGameWhenEdited();
            }
        };
        _depotGrid.CellContentClick += (_, cell) => RunDepotRowButton(cell.RowIndex, cell.ColumnIndex);

        _gameNameBox.Validated += (_, _) => SaveShownGameWhenEdited();
        _descriptionBox.Validated += (_, _) => SaveShownGameWhenEdited();
        _liveBranchBox.Validated += (_, _) => SaveShownGameWhenEdited();
        _uploadsNothingBox.Click += (_, _) => SaveShownGameWhenEdited();
        _addDepotButton.Click += (_, _) => AddDepot();
        _uploadButton.Click += async (_, _) => await UploadShownGameAsync();
        _stopButton.Click += (_, _) => _uploadCancellation?.Cancel();

        FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        buttons.Controls.Add(_addDepotButton);
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

    private static DataGridViewButtonColumn NewDepotRowButtonColumn(string buttonText)
    {
        return new DataGridViewButtonColumn
        {
            HeaderText = string.Empty,
            Text = buttonText,
            UseColumnTextForButtonValue = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
        };
    }

    private void RunDepotRowButton(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0)
        {
            return;
        }

        DataGridViewRow row = _depotGrid.Rows[rowIndex];
        if (columnIndex == BrowseContentFolderColumn)
        {
            BrowseDepotContentFolder(row);
        }
        else if (columnIndex == RemoveDepotColumn)
        {
            RemoveDepot(row);
        }
    }

    private void BrowseContentBuilderFolder()
    {
        string? pickedFolder = FolderPicker.Show(this,
            "Choose the ContentBuilder folder of the Steamworks SDK (sdk\\tools\\ContentBuilder)",
            _contentBuilderPathBox.Text);
        if (pickedFolder is not null)
        {
            _contentBuilderPathBox.Text = pickedFolder;
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

        if (folderPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            ShowGame(null);
            AppendLog($"{folderPath} is not a folder path.");

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
        AppendLog($"Found {_games.Count} game(s) in {contentBuilder.ScriptsFolder}.");
        foreach (string scriptLoadError in scriptLoadErrors)
        {
            AppendLog($"Could not read {scriptLoadError}");
        }

        ListGames(_games.Find(game => game.AppId == selectedAppId));
        if (_shownGame is null)
        {
            ShowGame(null);
        }
    }

    private string GameListText(AppBuildScript game)
    {
        return $"{_settings.GameName(game.AppId)} ({game.AppId})";
    }

    private void ListGames(AppBuildScript? gameToSelect)
    {
        object[] listedGames = ListedGames.MatchingFilterInSortOrder(_games, GameListText, _gameFilterBox.Text,
            _settings.GameSortOrder).ToArray();
        _ignoreGameSelectionChange = true;
        _gameList.BeginUpdate();
        _gameList.Items.Clear();
        _gameList.Items.AddRange(listedGames);
        if (gameToSelect is not null && _gameList.Items.Contains(gameToSelect))
        {
            _gameList.SelectedItem = gameToSelect;
        }
        else if (_gameList.Items.Count > 0)
        {
            _gameList.SelectedIndex = 0;
        }

        _gameList.EndUpdate();
        _ignoreGameSelectionChange = false;
        SwitchToSelectedGame();
    }

    private void ShowInGameList(AppBuildScript game)
    {
        if (!_gameList.Items.Contains(game))
        {
            _gameFilterBox.Text = string.Empty;
        }

        _gameList.SelectedItem = game;
    }

    private void SaveSortOrderAndListGames()
    {
        _settings.GameSortOrder = (GameSortOrder)_gameSortOrderBox.SelectedIndex;
        SaveSettings();
        ListGames(_shownGame);
    }

    private void SwitchToSelectedGame()
    {
        if (_ignoreGameSelectionChange)
        {
            return;
        }

        AppBuildScript? selectedGame = (AppBuildScript?)_gameList.SelectedItem;
        if (ReferenceEquals(selectedGame, _shownGame))
        {
            return;
        }

        if (_shownGame is not null && HasUnsavedEdits(_shownGame) && !TrySaveShownGame(out string whyNotSaved))
        {
            MessageBox.Show(this, EditsNotSavedMessage(_shownGame, whyNotSaved), Text, MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            ShowInGameList(_shownGame);

            return;
        }

        ShowGame(selectedGame);
    }

    private void ShowGame(AppBuildScript? game)
    {
        _shownGame = null;
        _depotGrid.Rows.Clear();
        _shownGame = game;
        bool hasGame = game is not null;
        _removeGameButton.Enabled = hasGame;
        _gameNameBox.Enabled = hasGame;
        _descriptionBox.Enabled = hasGame;
        _liveBranchBox.Enabled = hasGame;
        _uploadsNothingBox.Enabled = hasGame;
        _depotGrid.Enabled = hasGame;
        _addDepotButton.Enabled = hasGame;
        _uploadButton.Enabled = hasGame && _uploadCancellation is null;
        if (game is null)
        {
            _gameNameBox.Text = string.Empty;
            _appIdBox.Text = string.Empty;
            _descriptionBox.Text = string.Empty;
            _liveBranchBox.Text = string.Empty;
            _uploadsNothingBox.Checked = false;

            return;
        }

        _appIdBox.Text = game.AppId;
        foreach (DepotBuildScript depot in game.Depots)
        {
            AddDepotRow(depot);
        }

        ShowSavedValues(game);
    }

    private void ShowSavedValues(AppBuildScript game)
    {
        _gameNameBox.Text = _settings.GameName(game.AppId);
        _descriptionBox.Text = game.Description;
        _liveBranchBox.Text = game.LiveBranch;
        _uploadsNothingBox.Checked = game.UploadsNothing;
        foreach (DataGridViewRow row in _depotGrid.Rows)
        {
            if (ContentFolderInRow(row) != DepotInRow(row).ContentFolder)
            {
                row.Cells[ContentFolderColumn].Value = DepotInRow(row).ContentFolder;
                ShowContentSummary(row);
            }
        }
    }

    private DataGridViewRow AddDepotRow(DepotBuildScript depot)
    {
        DataGridViewRow row = _depotGrid.Rows[_depotGrid.Rows.Add(depot.DepotId, depot.ContentFolder)];
        row.Tag = depot;
        row.Cells[RemoveDepotColumn].ToolTipText = "Remove depot";
        ShowContentSummary(row);

        return row;
    }

    private void AddDepot()
    {
        if (_shownGame is null)
        {
            return;
        }

        using AddDepotForm dialog = new AddDepotForm { Icon = Icon };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        AppBuildScript game = _shownGame;
        string depotScriptPath = game.DepotScriptPath(dialog.DepotId);
        bool usesExistingDepotScript = File.Exists(depotScriptPath);
        DepotBuildScript depot;
        try
        {
            depot = game.AddDepotAndSave(dialog.DepotId);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or IOException
            or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"The depot was not added. {exception.Message}", Text, MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        _depotGrid.CurrentCell = AddDepotRow(depot).Cells[ContentFolderColumn];
        AppendLog($"Added depot {depot.DepotId} to {Path.GetFileName(game.FilePath)}. " + (usesExistingDepotScript
            ? $"It uses the {Path.GetFileName(depotScriptPath)} that was already in the scripts folder."
            : $"Wrote {Path.GetFileName(depotScriptPath)}."));
    }

    private void RemoveDepot(DataGridViewRow row)
    {
        AppBuildScript game = _shownGame!;
        DepotBuildScript depot = DepotInRow(row);
        string depotScriptName = Path.GetFileName(depot.FilePath);
        string depotScriptPathAddDepotUses = game.DepotScriptPath(depot.DepotId);
        string whereTheDepotScriptStays = string.Equals(depot.FilePath, depotScriptPathAddDepotUses,
            StringComparison.OrdinalIgnoreCase)
            ? $"{depotScriptName} stays in the scripts folder."
            : $"Its script stays at {depot.FilePath}. Add depot... can't add that script back, because it only uses "
            + $"{Path.GetFileName(depotScriptPathAddDepotUses)} next to the app script.";
        if (MessageBox.Show(this,
                $"Remove depot {depot.DepotId} from {_settings.GameName(game.AppId)} ({game.AppId})? "
                + whereTheDepotScriptStays,
                Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }

        try
        {
            game.RemoveDepotAndSave(depot);
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"The depot was not removed. {exception.Message}", Text, MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        _depotGrid.Rows.Remove(row);
        AppendLog($"Removed depot {depot.DepotId} from {Path.GetFileName(game.FilePath)}. {depotScriptName} was kept.");
    }

    private static DepotBuildScript DepotInRow(DataGridViewRow row)
    {
        return (DepotBuildScript)row.Tag!;
    }

    private static string ContentFolderInRow(DataGridViewRow row)
    {
        return row.Cells[ContentFolderColumn].Value as string ?? string.Empty;
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

    private void BrowseDepotContentFolder(DataGridViewRow row)
    {
        string? pickedFolder = FolderPicker.Show(this,
            $"Choose the build folder to upload to depot {DepotInRow(row).DepotId}",
            _shownGame!.AbsoluteContentFolder(DepotInRow(row), ContentFolderInRow(row)));
        if (pickedFolder is not null)
        {
            row.Cells[ContentFolderColumn].Value = pickedFolder;
            ShowContentSummary(row);
            SaveShownGameWhenEdited();
        }
    }

    private bool HasUnsavedEdits(AppBuildScript game)
    {
        if (_gameNameBox.Text != _settings.GameName(game.AppId)
            || _descriptionBox.Text != game.Description
            || _liveBranchBox.Text != game.LiveBranch
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

    private void SaveShownGameWhenEdited()
    {
        if (_shownGame is not null && HasUnsavedEdits(_shownGame) && !TrySaveShownGame(out string whyNotSaved))
        {
            AppendLog(EditsNotSavedMessage(_shownGame, whyNotSaved));
        }
    }

    private string EditsNotSavedMessage(AppBuildScript game, string whyNotSaved)
    {
        return $"The edits to {GameListText(game)} were not saved. {whyNotSaved}";
    }

    private bool TrySaveShownGame(out string whyNotSaved)
    {
        AppBuildScript game = _shownGame!;
        bool hasEdits = HasUnsavedEdits(game);
        game.Description = _descriptionBox.Text;
        if (_liveBranchBox.Text != game.LiveBranch)
        {
            game.LiveBranch = _liveBranchBox.Text.Trim();
        }

        game.UploadsNothing = _uploadsNothingBox.Checked;
        foreach (DataGridViewRow row in _depotGrid.Rows)
        {
            if (ContentFolderInRow(row) != DepotInRow(row).ContentFolder)
            {
                DepotInRow(row).ContentFolder = ContentFolderInRow(row).Trim();
            }
        }

        try
        {
            game.Save();
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
        {
            game.UndoUnsavedEdits();
            whyNotSaved = exception.Message;

            return false;
        }

        bool savedTheName = true;
        if (_gameNameBox.Text != _settings.GameName(game.AppId))
        {
            _settings.GameNamesByAppId[game.AppId] = _gameNameBox.Text.Trim();
            savedTheName = SaveSettings();
            int listIndex = _gameList.Items.IndexOf(game);
            if (listIndex >= 0)
            {
                _ignoreGameSelectionChange = true;
                _gameList.Items[listIndex] = game;
                _ignoreGameSelectionChange = false;
            }
        }

        ShowSavedValues(game);
        if (hasEdits && savedTheName)
        {
            AppendLog($"Saved the edits to {GameListText(game)}.");
        }

        whyNotSaved = string.Empty;

        return true;
    }

    private bool SaveSettings()
    {
        _settings.ContentBuilderPath = _contentBuilderPathBox.Text.Trim();
        _settings.SteamLogin = _steamLoginBox.Text.Trim();
        try
        {
            _settings.Save(_settingsPath);

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppendLog($"The settings were not saved. {exception.Message}");

            return false;
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

        using AddGameForm dialog = new AddGameForm { Icon = Icon };
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
        AppBuildScript? addedGame = _games.Find(game => game.AppId == dialog.AppId);
        if (addedGame is not null)
        {
            ShowInGameList(addedGame);
        }
    }

    private void RemoveShownGame()
    {
        if (_shownGame is null || _contentBuilder is null)
        {
            return;
        }

        AppBuildScript game = _shownGame;
        ContentBuilderFolder contentBuilder = _contentBuilder;
        string gameListText = GameListText(game);
        List<string> scriptPaths;
        try
        {
            scriptPaths = contentBuilder.AppScriptAndDepotScriptsOnlyItUses(game.FilePath, game.AppId);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException
            or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"The game was not removed. {exception.Message}", Text, MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        string scriptNames = string.Join(", ", scriptPaths.Select(Path.GetFileName));
        if (MessageBox.Show(this,
                $"Remove {gameListText} from the list? This moves {scriptNames} to "
                + $"{contentBuilder.RemovedGameScriptsFolder}. To list the game again, move the files back and click "
                + "Reload.",
                Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }

        try
        {
            contentBuilder.MoveToRemovedGameScriptsFolder(scriptPaths);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException
            or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"The game was not removed. {exception.Message}", Text, MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        ReloadGames();
        AppendLog($"Removed {gameListText}. Moved {scriptNames} to {contentBuilder.RemovedGameScriptsFolder}.");
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
        if (_uploadCancellation is not null)
        {
            AppendLog("Sign in is not available while steamcmd uploads. Its lines would appear in this log.");

            return;
        }

        if (!CanRunSteamCmdOrExplainWhyNot())
        {
            return;
        }

        if (_signInConsole is { HasExited: false })
        {
            AppendLog("The steamcmd sign-in window is already open.");

            return;
        }

        SaveSettings();
        _signInConsole?.Dispose();
        _signInConsole = null;
        try
        {
            _signInConsole = SteamCmd.OpenSignInConsole(_contentBuilder!.SteamCmdPath, _steamLoginBox.Text.Trim());
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or Win32Exception)
        {
            AppendLog($"steamcmd could not be started. {exception.Message}");

            return;
        }

        AppendLog("Opened steamcmd in its own window. Type your password and Steam Guard code there. "
            + "steamcmd keeps the sign-in for later uploads.");
    }

    private async Task UploadShownGameAsync()
    {
        _depotGrid.EndEdit();
        if (_shownGame is null || !CanRunSteamCmdOrExplainWhyNot())
        {
            return;
        }

        if (_signInConsole is { HasExited: false } && string.Equals(_signInConsole.StartInfo.FileName,
                _contentBuilder!.SteamCmdPath, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Finish the sign-in in the steamcmd window or close that window, then upload.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            return;
        }

        if (!TrySaveShownGame(out string whyNotSaved))
        {
            MessageBox.Show(this, $"The upload was not started because the build scripts were not saved. {whyNotSaved}",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);

            return;
        }

        SaveSettings();
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

        string buildLogPath = game.BuildLogPath;
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
        catch (SteamCmdNotStoppedException exception)
        {
            AppendLog(exception.Message);
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
                bool shownGameIsSelectedInList = ReferenceEquals(_gameList.SelectedItem, _shownGame);
                if (_settings.GameSortOrder == GameSortOrder.LastBuildFirst && shownGameIsSelectedInList)
                {
                    ListGames(_shownGame);
                }
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
                AppendLog(newText.Replace("\r\n", "\n").Replace('\r', '\n')
                    .Replace("\n", Environment.NewLine));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
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

    private void SaveEditsAndSettingsOrCancelClosing(FormClosingEventArgs closing)
    {
        _depotGrid.EndEdit();
        if (_shownGame is not null && HasUnsavedEdits(_shownGame) && !TrySaveShownGame(out string whyNotSaved)
            && MessageBox.Show(this,
                $"{EditsNotSavedMessage(_shownGame, whyNotSaved)} Close the window and lose the edits?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            closing.Cancel = true;

            return;
        }

        _uploadCancellation?.Cancel();
        SaveSettings();
    }
}
