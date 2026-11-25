namespace DepotUploader;

internal sealed class UploadConfirmationForm : Form
{
    private const int SpaceAroundContent = 10;

    private readonly Label _questionLabel = new Label
    {
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 8),
        UseMnemonic = false,
    };
    private readonly TextBox _detailsBox = new TextBox
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        WordWrap = false,
        ScrollBars = ScrollBars.Both,
        BackColor = SystemColors.Window,
        AccessibleName = "Games and depots",
    };
    private readonly FlowLayoutPanel _buttons = new FlowLayoutPanel
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.RightToLeft,
        AutoSize = true,
    };

    public UploadConfirmationForm(string question, string details, bool everyGameIsAPreviewBuild)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = everyGameIsAPreviewBuild ? "Confirm preview build" : "Confirm upload";
        Font = SystemFonts.MessageBoxFont;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(360, 200);
        _questionLabel.Text = question;
        _detailsBox.Text = details;

        IconButton confirmButton = everyGameIsAPreviewBuild
            ? new IconButton("Preview", "preview")
            : new IconButton("Upload", "upload");
        confirmButton.DialogResult = DialogResult.OK;
        IconButton cancelButton = new IconButton("Cancel") { DialogResult = DialogResult.Cancel };
        AcceptButton = confirmButton;
        CancelButton = cancelButton;
        _buttons.Controls.Add(cancelButton);
        _buttons.Controls.Add(confirmButton);

        TableLayoutPanel layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(SpaceAroundContent),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_questionLabel, 0, 0);
        layout.Controls.Add(_detailsBox, 0, 1);
        layout.Controls.Add(_buttons, 0, 2);
        Controls.Add(layout);
        DarkMode.ApplyToNewWindow(this);
        ActiveControl = confirmButton;
        ResumeLayout();
    }

    protected override void OnLoad(EventArgs load)
    {
        base.OnLoad(load);
        Size detailsText = TextRenderer.MeasureText(_detailsBox.Text, _detailsBox.Font);
        int spaceBesideDetailsText = LogicalToDeviceUnits(8);
        Size windowAroundDetailsText = Size - _detailsBox.ClientSize;
        int windowAroundQuestion = Width - _questionLabel.Parent!.DisplayRectangle.Width;
        int widthThatShowsEverything = Math.Max(
            windowAroundDetailsText.Width + detailsText.Width + spaceBesideDetailsText,
            windowAroundQuestion + _questionLabel.PreferredWidth);
        int heightThatShowsEverything =
            windowAroundDetailsText.Height + detailsText.Height + spaceBesideDetailsText;
        Size workingArea = Screen.FromControl(Owner ?? this).WorkingArea.Size;
        Size = new Size(
            Math.Min(widthThatShowsEverything, workingArea.Width * 4 / 5),
            Math.Min(heightThatShowsEverything, workingArea.Height * 4 / 5));
        CenterToParent();
    }
}
