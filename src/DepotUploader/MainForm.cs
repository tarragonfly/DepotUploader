using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms.VisualStyles;

namespace DepotUploader;

internal sealed class MainForm : Form
{
    private const int ContentFolderColumn = 1;
    private const int BrowseContentFolderColumn = 2;
    private const int ContentSummaryColumn = 3;
    private const int RemoveDepotColumn = 4;
    private const int DepotRowIconMargin = 3;
    private const string BrowseContentFolderButtonName = "Choose content folder";
    private const string RemoveDepotButtonName = "Remove depot";

    private readonly string _settingsPath = UploaderSettings.DefaultFilePath;
    private readonly UploaderSettings _settings;
    private readonly List<AppBuildScript> _games = new List<AppBuildScript>();
    private readonly HashSet<AppBuildScript> _gamesWithADepotWithoutFiles = new HashSet<AppBuildScript>();
    private readonly HashSet<string> _scriptPathsOfTickedGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
        DrawMode = DrawMode.OwnerDrawFixed,
    };
    private readonly IconButton _uploadTickedGamesButton = new IconButton("Upload ticked games", "upload")
    {
        Dock = DockStyle.Bottom,
        Enabled = false,
    };
    private readonly IconButton _removeGameButton = new IconButton("Remove game", "trash") { Dock = DockStyle.Bottom };
    private readonly TextBox _gameNameBox = new TextBox { Dock = DockStyle.Fill };
    private readonly TextBox _appIdBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly TextBox _descriptionBox = new TextBox { Dock = DockStyle.Fill };
    private readonly TextBox _liveBranchBox = new TextBox { Dock = DockStyle.Fill };
    private readonly CheckBox _uploadsNothingBox = new CheckBox { Text = "Preview only, upload nothing", AutoSize = true };
    private readonly DataGridView _depotGrid = new DataGridView();
    private readonly IconButton _addDepotButton = new IconButton("Add depot", "add");
    private readonly IconButton _uploadButton = new IconButton("Upload", "upload");
    private readonly IconButton _stopButton = new IconButton("Stop", "stop") { Enabled = false };
    private readonly PictureBox _uploadResultIcon = new PictureBox
    {
        SizeMode = PictureBoxSizeMode.AutoSize,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(12, 3, 0, 3),
        Visible = false,
    };
    private readonly Label _uploadResultLabel = new Label
    {
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        UseMnemonic = false,
    };
    private readonly ToolTip _namesOfIcons = new ToolTip();
    private readonly int _textLineIconPixels = SystemFonts.MessageBoxFont.Height;
    private readonly Bitmap _stopIcon;
    private readonly Bitmap _successIcon;
    private readonly Bitmap _failureIcon;
    private readonly Bitmap _folderIcon;
    private readonly Bitmap _trashIcon;
    private readonly Bitmap _warningIcon;
    private readonly Bitmap _gameListWarningIcon;
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
    private Task<AppBuildScript[]>? _newestSearchForDepotsWithoutFiles;
    private bool _ignoreGameSelectionChange;

    public MainForm()
    {
        _settings = UploaderSettings.Load(_settingsPath, out string? settingsLoadError);
        _stopIcon = TextLineIcon("stop");
        _successIcon = TextLineIcon("success");
        _failureIcon = TextLineIcon("failure");
        _gameList.ItemHeight = RowHeightOfAStandardList(SystemFonts.MessageBoxFont);
        _gameListWarningIcon = EmbeddedIcon.LargestThatFits("warning", _gameList.ItemHeight - 2);
        int depotRowIconPixels = _depotGrid.RowTemplate.Height - 2 * DepotRowIconMargin;
        _folderIcon = EmbeddedIcon.LargestThatFits("folder", depotRowIconPixels);
        _trashIcon = EmbeddedIcon.LargestThatFits("trash", depotRowIconPixels);
        _warningIcon = EmbeddedIcon.LargestThatFits("warning", depotRowIconPixels);

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
        _gameList.DrawItem += (_, item) => DrawGameListItem(item);
        _gameList.SelectedIndexChanged += (_, _) => SwitchToSelectedGame();
        _gameList.MouseDown += (_, mouse) => TickOrUntickGameWhoseCheckBoxIsClicked(mouse);
        _gameList.KeyDown += (_, key) => TickOrUntickSelectedGameOnSpace(key);
        FormClosing += (_, closing) => SaveEditsAndSettingsOrCancelClosing(closing);
        Shown += (_, _) => ReloadGames();
    }

    private Control BuildSteamSetupPanel()
    {
        IconButton browseButton = new IconButton("Browse", "folder");
        browseButton.Click += (_, _) => BrowseContentBuilderFolder();
        IconButton reloadButton = new IconButton("Reload", "reload") { CausesValidation = false };
        reloadButton.Click += (_, _) => ReloadGames();
        IconButton signInButton = new IconButton("Sign in", "valve");
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
        IconButton addGameButton = new IconButton("Add game", "add") { Dock = DockStyle.Bottom };
        addGameButton.Click += (_, _) => AddGame();
        _removeGameButton.Click += (_, _) => RemoveShownGame();
        _uploadTickedGamesButton.Click += async (_, _) => await UploadTickedGamesAsync();
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
        filterAndSortOrder.Controls.Add(NewIconThatNames(_gameFilterBox, "filter", "Filter"), 0, 0);
        filterAndSortOrder.Controls.Add(_gameFilterBox, 1, 0);
        filterAndSortOrder.Controls.Add(NewIconThatNames(_gameSortOrderBox, "sort", "Sort by"), 0, 1);
        filterAndSortOrder.Controls.Add(_gameSortOrderBox, 1, 1);

        Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        panel.Controls.Add(_gameList);
        panel.Controls.Add(filterAndSortOrder);
        panel.Controls.Add(_uploadTickedGamesButton);
        panel.Controls.Add(addGameButton);
        panel.Controls.Add(_removeGameButton);
        filterAndSortOrder.TabIndex = 0;
        _gameList.TabIndex = 1;
        _uploadTickedGamesButton.TabIndex = 2;
        addGameButton.TabIndex = 3;
        _removeGameButton.TabIndex = 4;

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
        _depotGrid.Columns.Add(NewDepotRowIconButtonColumn(BrowseContentFolderButtonName, _folderIcon));
        _depotGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Contents",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Padding = new Padding(_warningIcon.Width + 2 * DepotRowIconMargin, 0, 0, 0),
            },
        });
        _depotGrid.Columns.Add(NewDepotRowIconButtonColumn(RemoveDepotButtonName, _trashIcon));
        _depotGrid.CellEndEdit += (_, cell) =>
        {
            if (_shownGame is not null)
            {
                ShowContentSummary(_depotGrid.Rows[cell.RowIndex]);
                SaveShownGameWhenEdited();
            }
        };
        _depotGrid.CellContentClick += (_, cell) => RunDepotRowButton(cell.RowIndex, cell.ColumnIndex);
        _depotGrid.CellPainting += (_, cell) => PaintDepotRowIcon(cell);

        _gameNameBox.Validated += (_, _) => SaveShownGameWhenEdited();
        _descriptionBox.Validated += (_, _) => SaveShownGameWhenEdited();
        _liveBranchBox.Validated += (_, _) => SaveShownGameWhenEdited();
        _uploadsNothingBox.Click += (_, _) => SaveShownGameWhenEdited();
        _uploadsNothingBox.CheckedChanged += (_, _) => ShowUploadOrPreviewOnUploadButton();
        _addDepotButton.Click += (_, _) => AddDepot();
        _uploadButton.Click += async (_, _) => await UploadShownGameAsync();
        _stopButton.Click += (_, _) => _uploadCancellation?.Cancel();

        FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        buttons.Controls.Add(_addDepotButton);
        buttons.Controls.Add(_uploadButton);
        buttons.Controls.Add(_stopButton);
        buttons.Controls.Add(_uploadResultIcon);
        buttons.Controls.Add(_uploadResultLabel);

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

    private Bitmap TextLineIcon(string name)
    {
        return EmbeddedIcon.LargestThatFits(name, _textLineIconPixels);
    }

    private static int RowHeightOfAStandardList(Font font)
    {
        using ListBox standardList = new ListBox { Font = font };
        standardList.CreateControl();

        return standardList.ItemHeight;
    }

    private PictureBox NewIconThatNames(Control namedControl, string iconName, string name)
    {
        namedControl.AccessibleName = name;
        PictureBox icon = new PictureBox
        {
            Image = TextLineIcon(iconName),
            SizeMode = PictureBoxSizeMode.AutoSize,
            Anchor = AnchorStyles.None,
        };
        _namesOfIcons.SetToolTip(icon, name);

        return icon;
    }

    private void DrawGameListItem(DrawItemEventArgs item)
    {
        if (item.Index < 0)
        {
            return;
        }

        item.DrawBackground();
        AppBuildScript game = (AppBuildScript)_gameList.Items[item.Index];
        Rectangle checkBoxBounds = GameCheckBoxBounds(item.Graphics, item.Bounds);
        item.Graphics.FillRectangle(SystemBrushes.Window, Rectangle.Inflate(checkBoxBounds, 1, 1));
        CheckBoxRenderer.DrawCheckBox(item.Graphics, checkBoxBounds.Location,
            _scriptPathsOfTickedGames.Contains(game.FilePath)
                ? CheckBoxState.CheckedNormal
                : CheckBoxState.UncheckedNormal);
        Rectangle textBounds = Rectangle.FromLTRB(checkBoxBounds.Right + DepotRowIconMargin, item.Bounds.Top,
            item.Bounds.Right, item.Bounds.Bottom);
        if (_gamesWithADepotWithoutFiles.Contains(game))
        {
            textBounds.Width -= _gameListWarningIcon.Width + 2 * DepotRowIconMargin;
            int top = item.Bounds.Top + (item.Bounds.Height - _gameListWarningIcon.Height) / 2;
            item.Graphics.DrawImage(_gameListWarningIcon, new Rectangle(textBounds.Right + DepotRowIconMargin, top,
                _gameListWarningIcon.Width, _gameListWarningIcon.Height));
        }

        TextRenderer.DrawText(item.Graphics, _gameList.GetItemText(game), item.Font, textBounds, item.ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix);
        item.DrawFocusRectangle();
    }

    private static Rectangle GameCheckBoxBounds(Graphics graphics, Rectangle rowBounds)
    {
        Size checkBoxSize = CheckBoxRenderer.GetGlyphSize(graphics, CheckBoxState.UncheckedNormal);

        return new Rectangle(rowBounds.Left + DepotRowIconMargin,
            rowBounds.Top + (rowBounds.Height - checkBoxSize.Height) / 2, checkBoxSize.Width, checkBoxSize.Height);
    }

    private void TickOrUntickGameWhoseCheckBoxIsClicked(MouseEventArgs mouse)
    {
        int rowIndex = _gameList.IndexFromPoint(mouse.Location);
        if (mouse.Button != MouseButtons.Left || rowIndex < 0 || rowIndex >= _gameList.Items.Count)
        {
            return;
        }

        using Graphics graphics = _gameList.CreateGraphics();
        Rectangle checkBoxBounds = GameCheckBoxBounds(graphics, _gameList.GetItemRectangle(rowIndex));
        if (mouse.X <= checkBoxBounds.Right + DepotRowIconMargin)
        {
            TickOrUntick((AppBuildScript)_gameList.Items[rowIndex]);
        }
    }

    private void TickOrUntickSelectedGameOnSpace(KeyEventArgs key)
    {
        if (key.KeyCode == Keys.Space && _gameList.SelectedItem is AppBuildScript game)
        {
            TickOrUntick(game);
            key.Handled = true;
            key.SuppressKeyPress = true;
        }
    }

    private void TickOrUntick(AppBuildScript game)
    {
        if (!_scriptPathsOfTickedGames.Remove(game.FilePath))
        {
            _scriptPathsOfTickedGames.Add(game.FilePath);
        }

        _gameList.Invalidate();
        ShowWhichUploadsCanStart();
    }

    private AppBuildScript[] TickedGamesInListOrder()
    {
        IEnumerable<AppBuildScript> tickedGames =
            _games.Where(game => _scriptPathsOfTickedGames.Contains(game.FilePath));

        return ListedGames.MatchingFilterInSortOrder(tickedGames, GameListText, string.Empty, _settings.GameSortOrder)
            .ToArray();
    }

    private void ShowWhichUploadsCanStart()
    {
        int tickedGameCount = _games.Count(game => _scriptPathsOfTickedGames.Contains(game.FilePath));
        bool noUploadIsRunning = _uploadCancellation is null;
        _uploadButton.Enabled = _shownGame is not null && noUploadIsRunning;
        _uploadTickedGamesButton.Enabled = tickedGameCount > 0 && noUploadIsRunning;
        _uploadTickedGamesButton.Text = tickedGameCount switch
        {
            0 => "Upload ticked games",
            1 => "Upload 1 ticked game",
            _ => $"Upload {tickedGameCount} ticked games",
        };
    }

    private async void ShowWhichGamesHaveADepotWithoutFiles()
    {
        (AppBuildScript Game, string[] ContentFolders)[] gamesAndContentFolders = _games
            .Select(game => (game, game.Depots.Select(depot => game.AbsoluteContentFolder(depot)).ToArray()))
            .ToArray();
        Task<AppBuildScript[]> search = Task.Run(() => gamesAndContentFolders
            .Where(entry => entry.ContentFolders.Any(folder => !ContentFolderSummary.FolderHasFiles(folder)))
            .Select(entry => entry.Game)
            .ToArray());
        _newestSearchForDepotsWithoutFiles = search;
        AppBuildScript[] gamesWithADepotWithoutFiles = await search;
        if (IsDisposed || search != _newestSearchForDepotsWithoutFiles)
        {
            return;
        }

        _gamesWithADepotWithoutFiles.Clear();
        _gamesWithADepotWithoutFiles.UnionWith(gamesWithADepotWithoutFiles);
        _gameList.Invalidate();
    }

    private static DataGridViewButtonColumn NewDepotRowIconButtonColumn(string buttonName, Bitmap icon)
    {
        return new DataGridViewButtonColumn
        {
            HeaderText = string.Empty,
            Text = buttonName,
            UseColumnTextForButtonValue = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Resizable = DataGridViewTriState.False,
            Width = icon.Width + 4 * DepotRowIconMargin,
        };
    }

    private void PaintDepotRowIcon(DataGridViewCellPaintingEventArgs cell)
    {
        if (cell.RowIndex < 0)
        {
            return;
        }

        if (cell.ColumnIndex == ContentSummaryColumn)
        {
            if (cell.Value is ContentFolderSummary { HasFiles: false })
            {
                cell.Paint(cell.ClipBounds, DataGridViewPaintParts.All);
                DrawIconInDepotRow(cell, _warningIcon, cell.CellBounds.Left + DepotRowIconMargin);
            }

            return;
        }

        if (cell.ColumnIndex is BrowseContentFolderColumn or RemoveDepotColumn)
        {
            Bitmap icon = cell.ColumnIndex == BrowseContentFolderColumn ? _folderIcon : _trashIcon;
            cell.Paint(cell.ClipBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);
            DrawIconInDepotRow(cell, icon, cell.CellBounds.Left + (cell.CellBounds.Width - icon.Width) / 2);
        }
    }

    private static void DrawIconInDepotRow(DataGridViewCellPaintingEventArgs cell, Bitmap icon, int left)
    {
        int top = cell.CellBounds.Top + (cell.CellBounds.Height - icon.Height) / 2;
        cell.Graphics.DrawImage(icon, new Rectangle(left, top, icon.Width, icon.Height));
        cell.Handled = true;
    }

    private void ShowUploadResult(Image? icon, string text)
    {
        _uploadResultIcon.Image = icon;
        _uploadResultIcon.Visible = icon is not null;
        _uploadResultLabel.Text = text;
    }

    private void ShowUploadOrPreviewOnUploadButton()
    {
        _uploadButton.Text = _uploadsNothingBox.Checked ? "Preview" : "Upload";
        _uploadButton.ShowIcon(_uploadsNothingBox.Checked ? "preview" : "upload");
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
        _scriptPathsOfTickedGames.IntersectWith(_games.Select(game => game.FilePath));
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

        ShowWhichGamesHaveADepotWithoutFiles();
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
        ShowWhichUploadsCanStart();
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
        row.Cells[BrowseContentFolderColumn].ToolTipText = BrowseContentFolderButtonName;
        row.Cells[RemoveDepotColumn].ToolTipText = RemoveDepotButtonName;
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
        ShowWhichGamesHaveADepotWithoutFiles();
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
            : $"Its script stays at {depot.FilePath}. Add depot can't add that script back, because it only uses "
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
        ShowWhichGamesHaveADepotWithoutFiles();
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
        ContentFolderSummary summary =
            await Task.Run(() => ContentFolderSummary.DescribeOrGiveReadError(absoluteFolder));
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
        if (hasEdits)
        {
            ShowWhichGamesHaveADepotWithoutFiles();
        }

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
            AppendLog("Sign in is not available while an upload is prepared or runs. "
                + "The lines of steamcmd would appear in this log.");

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
        if (_shownGame is not null)
        {
            await UploadGamesAsync(new[] { _shownGame });
        }
    }

    private async Task UploadTickedGamesAsync()
    {
        AppBuildScript[] games = TickedGamesInListOrder();
        if (games.Length > 0)
        {
            await UploadGamesAsync(games);
        }
    }

    private async Task UploadGamesAsync(IReadOnlyList<AppBuildScript> games)
    {
        _depotGrid.EndEdit();
        if (!CanRunSteamCmdOrExplainWhyNot())
        {
            return;
        }

        string steamCmdPath = _contentBuilder!.SteamCmdPath;
        if (_signInConsole is { HasExited: false } && string.Equals(_signInConsole.StartInfo.FileName,
                steamCmdPath, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Finish the sign-in in the steamcmd window or close that window, then upload.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            return;
        }

        foreach (AppBuildScript game in games)
        {
            if (!TrySaveBeforeUpload(game, out string whyNotSaved))
            {
                MessageBox.Show(this,
                    $"The upload was not started because the build scripts of {GameListText(game)} were not saved. "
                    + whyNotSaved,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);

                return;
            }
        }

        SaveSettings();
        string steamLogin = _steamLoginBox.Text.Trim();
        string scriptCopiesFolder = Path.Combine(Path.GetTempPath(), "DepotUploader", Guid.NewGuid().ToString("N"));
        Image? resultIconOfLastUpload = _uploadResultIcon.Image;
        string resultOfLastUpload = _uploadResultLabel.Text;
        CancellationTokenSource uploadCancellation = new CancellationTokenSource();
        _uploadCancellation = uploadCancellation;
        _stopButton.Enabled = true;
        ShowWhichUploadsCanStart();
        try
        {
            GameUpload[] uploads;
            try
            {
                uploads = games.Select((game, index) => GameUpload.FromScripts(game, _settings.GameName(game.AppId),
                    Path.Combine(scriptCopiesFolder, (index + 1).ToString(CultureInfo.InvariantCulture)))).ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                or InvalidOperationException)
            {
                MessageBox.Show(this, $"The upload was not started. {exception.Message}", Text, MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            if (GameUpload.WhyTwoUploadsWouldSetTheSameBranchLive(uploads) is string whyNotStarted)
            {
                MessageBox.Show(this, $"The upload was not started. {whyNotStarted}", Text, MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            ShowUploadResult(null, "Counting files");
            ContentFolderSummary[][]? confirmedContent;
            try
            {
                confirmedContent =
                    await GameUpload.DescribeContentUnlessStoppedAsync(uploads, uploadCancellation.Token);
            }
            catch (IOException exception)
            {
                if (!IsDisposed)
                {
                    ShowUploadResult(resultIconOfLastUpload, resultOfLastUpload);
                    MessageBox.Show(this, $"The upload was not started. {exception.Message}", Text,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                return;
            }

            if (IsDisposed)
            {
                return;
            }

            ShowUploadResult(resultIconOfLastUpload, resultOfLastUpload);
            if (confirmedContent is null)
            {
                return;
            }

            using (UploadConfirmationForm confirmation = new UploadConfirmationForm(
                       UploadConfirmation.Question(uploads), UploadConfirmation.Details(uploads, confirmedContent),
                       uploads.All(upload => upload.UploadsNothing)) { Icon = Icon })
            {
                if (confirmation.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            ShowUploadResult(null, string.Empty);
            UploadOutcome[] outcomes = Enumerable.Repeat(UploadOutcome.NotStarted, uploads.Length).ToArray();
            string outcomeInResultLine = string.Empty;
            bool triedToStartSteamCmd = false;
            for (int i = 0; i < uploads.Length && !uploadCancellation.IsCancellationRequested; i++)
            {
                string positionInBatch = uploads.Length == 1 ? string.Empty : $", game {i + 1} of {uploads.Length}";
                if (uploads.Length > 1 && !IsDisposed)
                {
                    ShowUploadResult(null, $"Game {i + 1} of {uploads.Length}: {uploads[i].GameName}");
                }

                bool triedToStartSteamCmdForThisGame;
                (outcomes[i], outcomeInResultLine, triedToStartSteamCmdForThisGame) =
                    await UploadGameUnlessItsFilesChangedAsync(uploads[i], confirmedContent[i], positionInBatch,
                        steamCmdPath, steamLogin, uploadCancellation.Token);
                triedToStartSteamCmd |= triedToStartSteamCmdForThisGame;
            }

            if (IsDisposed)
            {
                return;
            }

            string uploadOrPreview = uploads[0].UploadsNothing ? "Preview build" : "Upload";
            ShowUploadResult(ResultIcon(outcomes), uploads.Length == 1
                ? $"{uploadOrPreview} of {uploads[0].GameName} {outcomeInResultLine} at {DateTime.Now:HH:mm}"
                : BatchResultLine.Text(
                    uploads.Zip(outcomes, (upload, outcome) => (upload.GameName, outcome)).ToArray(), DateTime.Now));
            bool shownGameIsSelectedInList = ReferenceEquals(_gameList.SelectedItem, _shownGame);
            if (triedToStartSteamCmd && _settings.GameSortOrder == GameSortOrder.LastBuildFirst
                && shownGameIsSelectedInList)
            {
                ListGames(_shownGame);
            }
        }
        finally
        {
            uploadCancellation.Dispose();
            _uploadCancellation = null;
            DeleteScriptCopies(scriptCopiesFolder);
            if (!IsDisposed)
            {
                _stopButton.Enabled = false;
                ShowWhichUploadsCanStart();
            }
        }
    }

    private bool TrySaveBeforeUpload(AppBuildScript game, out string whyNotSaved)
    {
        if (ReferenceEquals(game, _shownGame))
        {
            return TrySaveShownGame(out whyNotSaved);
        }

        try
        {
            game.Save();
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
        {
            whyNotSaved = exception.Message;

            return false;
        }

        whyNotSaved = string.Empty;

        return true;
    }

    private async Task<(UploadOutcome Outcome, string OutcomeInResultLine, bool TriedToStartSteamCmd)>
        UploadGameUnlessItsFilesChangedAsync(GameUpload upload, ContentFolderSummary[] confirmedContent,
            string positionInBatch, string steamCmdPath, string steamLogin, CancellationToken stopRequested)
    {
        ContentFolderSummary[][]? contentNow;
        try
        {
            contentNow = await GameUpload.DescribeContentUnlessStoppedAsync(new[] { upload }, stopRequested);
        }
        catch (IOException exception)
        {
            AppendLog($"{upload.GameName} ({upload.AppId}) was not uploaded. {exception.Message}");

            return (UploadOutcome.Failed, "failed", false);
        }

        if (contentNow is null)
        {
            return (UploadOutcome.NotStarted, "stopped", false);
        }

        for (int i = 0; i < upload.Depots.Count; i++)
        {
            if (contentNow[0][i] != confirmedContent[i])
            {
                AppendLog($"{upload.GameName} ({upload.AppId}) was not uploaded. The files of depot "
                    + $"{upload.Depots[i].DepotId} changed after the confirmation. Confirmed: "
                    + $"{confirmedContent[i].TextWithExactSizeAndTime}. Now: {contentNow[0][i].TextWithExactSizeAndTime}.");

                return (UploadOutcome.Failed, "failed", false);
            }
        }

        AppendLog($"Starting steamcmd for {upload.GameName} ({upload.AppId}){positionInBatch}.");
        long? buildLogLengthBefore = LengthOfFileOrNullWhenItCannotBeRead(upload.BuildLogPath);
        try
        {
            int exitCode = await SteamCmd.RunAppBuildAsync(steamCmdPath, steamLogin, upload.AppScriptCopyPath,
                AppendLogFromAnyThread, stopRequested);
            if (buildLogLengthBefore is long lengthBefore)
            {
                AppendBuildLogWrittenAfter(upload.BuildLogPath, lengthBefore);
            }
            else
            {
                AppendLog($"{Path.GetFileName(upload.BuildLogPath)} is not shown here. Its length could not be "
                    + "read before steamcmd started.");
            }

            AppendLog(exitCode == 0
                ? "steamcmd finished with exit code 0."
                : $"steamcmd failed with exit code {exitCode}. If it could not sign in, use Sign in and upload again.");

            return exitCode == 0
                ? (UploadOutcome.Finished, "finished", true)
                : (UploadOutcome.Failed, $"failed with exit code {exitCode}", true);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Stopped steamcmd. If the upload was almost done, the build may still be in Steamworks.");

            return (UploadOutcome.Stopped, "stopped", true);
        }
        catch (SteamCmdNotStoppedException exception)
        {
            AppendLog(exception.Message);

            return (UploadOutcome.Failed, "failed", true);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException
            or Win32Exception)
        {
            AppendLog($"steamcmd could not be started. {exception.Message}");

            return (UploadOutcome.Failed, "failed", true);
        }
    }

    private static long? LengthOfFileOrNullWhenItCannotBeRead(string filePath)
    {
        try
        {
            return new FileInfo(filePath).Length;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException
            or ArgumentException or NotSupportedException)
        {
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private Bitmap ResultIcon(UploadOutcome[] outcomes)
    {
        if (outcomes.Contains(UploadOutcome.Stopped))
        {
            return _stopIcon;
        }

        if (outcomes.Contains(UploadOutcome.Failed))
        {
            return _failureIcon;
        }

        return outcomes.Contains(UploadOutcome.NotStarted) ? _stopIcon : _successIcon;
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
