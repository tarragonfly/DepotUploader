namespace DepotUploader;

internal sealed class TextFieldFrame : Panel
{
    private readonly TextBox _box;
    private readonly Bitmap? _icon;

    public TextFieldFrame(TextBox box, string? iconName = null)
    {
        _box = box;
        _icon = iconName is null ? null : EmbeddedIcon.LargestThatFits(iconName, WindowTheme.LineHeight);
        Dock = DockStyle.Fill;
        AutoSize = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        box.Dock = DockStyle.None;
        if (WindowTheme.DrawsControls)
        {
            box.BorderStyle = BorderStyle.None;
            Cursor = Cursors.IBeam;
            MouseDown += (_, _) => box.Focus();
            MouseEnter += (_, _) => ShowCurrentColors();
            MouseLeave += (_, _) => ShowCurrentColors();
            box.MouseEnter += (_, _) => ShowCurrentColors();
            box.MouseLeave += (_, _) => ShowCurrentColors();
            box.GotFocus += (_, _) => ShowCurrentColors();
            box.LostFocus += (_, _) => ShowCurrentColors();
            box.EnabledChanged += (_, _) => ShowCurrentColors();
            box.ReadOnlyChanged += (_, _) => ShowCurrentColors();
        }

        Controls.Add(box);
    }

    public void ShowCurrentColors()
    {
        ThemeColors colors = WindowTheme.Colors;
        Color fill = _box.Focused ? colors.EditedTextFieldFill : colors.TextFieldFill;
        if (!_box.Enabled)
        {
            fill = ThemeColors.OfDisabledControl(fill, colors.WindowFill);
        }
        else if (_box.ReadOnly)
        {
            fill = colors.ReadOnlyTextFieldFill;
        }
        else if (!_box.Focused && ClientRectangle.Contains(PointToClient(MousePosition)))
        {
            fill = ThemeColors.FillUnderMouse(fill, colors.TextFieldText);
        }

        _box.BackColor = fill;
        _box.ForeColor = colors.TextFieldText;
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int height = WindowTheme.DrawsControls ? WindowTheme.ControlHeight : _box.PreferredHeight;

        return new Size(4 * height, height);
    }

    protected override void OnLayout(LayoutEventArgs layout)
    {
        base.OnLayout(layout);
        int spaceBesideText = WindowTheme.DrawsControls ? WindowTheme.SpaceInsideOutline : 0;
        int left = _icon is null ? spaceBesideText : 2 * WindowTheme.SpaceInsideOutline + _icon.Width;
        _box.SetBounds(left, (ClientSize.Height - _box.Height) / 2, ClientSize.Width - left - spaceBesideText,
            _box.Height);
    }

    protected override void OnPaint(PaintEventArgs paint)
    {
        paint.Graphics.Clear(BackColor);
        Color iconColor = ForeColor;
        if (WindowTheme.DrawsControls)
        {
            ThemeColors colors = WindowTheme.Colors;
            Color outline = _box.Enabled ? colors.Outline : ThemeColors.OfDisabledControl(colors.Outline, BackColor);
            RoundedBox.Draw(paint.Graphics, ClientRectangle, WindowTheme.CornerRadius, _box.BackColor, outline);
            iconColor = _box.Enabled
                ? colors.TextFieldText
                : ThemeColors.OfDisabledControl(colors.TextFieldText, _box.BackColor);
        }

        if (_icon is not null)
        {
            EmbeddedIcon.Draw(paint.Graphics, _icon,
                new Point(WindowTheme.SpaceInsideOutline, (ClientSize.Height - _icon.Height) / 2), iconColor);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _icon?.Dispose();
        }

        base.Dispose(disposing);
    }
}
