namespace DepotUploader;

internal sealed class AddDepotForm : Form
{
    private readonly TextBox _depotIdBox = new TextBox { Dock = DockStyle.Fill };

    public AddDepotForm()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Add depot";
        Font = SystemFonts.MessageBoxFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(320, 90);

        IconButton addButton = new IconButton("Add", "add");
        addButton.Click += (_, _) => CloseWhenValid();
        IconButton cancelButton = new IconButton("Cancel") { DialogResult = DialogResult.Cancel };
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
            RowCount = 2,
            Padding = new Padding(10),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Depot ID", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_depotIdBox, 1, 0);
        layout.Controls.Add(buttons, 0, 1);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        DarkMode.ApplyToNewWindow(this);
        ResumeLayout();
    }

    public string DepotId => _depotIdBox.Text.Trim();

    private void CloseWhenValid()
    {
        if (!AddGameForm.IsNumber(DepotId))
        {
            MessageBox.Show(this, "Enter a numeric depot ID.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);

            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
