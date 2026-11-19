namespace DepotUploader;

internal sealed class AddGameForm : Form
{
    private readonly TextBox _gameNameBox = new TextBox { Dock = DockStyle.Fill };
    private readonly TextBox _appIdBox = new TextBox { Dock = DockStyle.Fill };
    private readonly TextBox _depotIdsBox = new TextBox { Dock = DockStyle.Fill };

    public AddGameForm()
    {
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Add game";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 170);

        Button addButton = new Button { Text = "Add", AutoSize = true };
        addButton.Click += (_, _) => CloseWhenValid();
        Button cancelButton = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        AcceptButton = addButton;
        CancelButton = cancelButton;

        FlowLayoutPanel buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(addButton);

        TableLayoutPanel layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(10),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, 0, "Name", _gameNameBox);
        AddRow(layout, 1, "App ID", _appIdBox);
        AddRow(layout, 2, "Depot IDs, comma separated", _depotIdsBox);
        layout.Controls.Add(buttons, 0, 3);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
    }

    public string GameName => _gameNameBox.Text.Trim();

    public string AppId => _appIdBox.Text.Trim();

    public List<string> DepotIds
    {
        get
        {
            List<string> depotIds = new List<string>();
            foreach (string part in _depotIdsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                depotIds.Add(part.Trim());
            }

            return depotIds;
        }
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control field)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        layout.Controls.Add(field, 1, row);
    }

    private static bool IsNumber(string text)
    {
        return text.Length > 0 && text.All(char.IsAsciiDigit);
    }

    private void CloseWhenValid()
    {
        List<string> depotIds = DepotIds;
        if (!IsNumber(AppId) || depotIds.Count == 0 || !depotIds.All(IsNumber))
        {
            MessageBox.Show(this, "Enter a numeric App ID and at least one numeric depot ID.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);

            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
