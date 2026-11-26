namespace DepotUploader;

internal sealed class IconButton : Button
{
    private static readonly int IconPixels = WindowTheme.LineHeight;
    private static readonly int SpaceLeftOfIcon = IconPixels / 3;
    private static readonly int SpaceBesideText = SpaceLeftOfIcon + IconPixels + IconPixels / 4;

    private Bitmap? _icon;

    public IconButton(string text, string? iconName = null)
    {
        Text = text;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        if (iconName is not null)
        {
            ShowIcon(iconName);
        }
    }

    public void ShowIcon(string iconName)
    {
        _icon?.Dispose();
        _icon = EmbeddedIcon.LargestThatFits(iconName, IconPixels);
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        return new Size(TextRenderer.MeasureText(Text, Font).Width + 2 * SpaceBesideText, WindowTheme.ControlHeight);
    }

    protected override void OnPaint(PaintEventArgs paint)
    {
        if (!WindowTheme.DrawsControls)
        {
            base.OnPaint(paint);
            DrawIcon(paint.Graphics, Enabled ? ForeColor : SystemColors.GrayText);

            return;
        }

        ThemeColors colors = WindowTheme.Colors;
        bool mouseIsOver = ClientRectangle.Contains(PointToClient(MousePosition));
        bool isHeldDown = mouseIsOver && Capture;
        bool confirmsDialogOnEnter = IsDefault && FindForm()?.AcceptButton is not null;
        bool looksSelected = isHeldDown || confirmsDialogOnEnter;
        Color fill = looksSelected ? colors.SelectedFill : colors.ButtonFill;
        Color textColor = looksSelected ? colors.SelectedText : colors.ButtonText;
        Color outline = IsDefault || (Focused && ShowFocusCues) ? textColor : colors.Outline;
        if (!Enabled)
        {
            fill = ThemeColors.OfDisabledControl(fill, BackColor);
            outline = ThemeColors.OfDisabledControl(outline, BackColor);
            textColor = ThemeColors.OfDisabledControl(textColor, fill);
        }
        else if (mouseIsOver && !isHeldDown)
        {
            fill = ThemeColors.FillUnderMouse(fill, textColor);
        }

        paint.Graphics.Clear(BackColor);
        RoundedBox.Draw(paint.Graphics, ClientRectangle, WindowTheme.CornerRadius, fill, outline);
        TextRenderer.DrawText(paint.Graphics, Text, Font, ClientRectangle, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        DrawIcon(paint.Graphics, textColor);
    }

    private void DrawIcon(Graphics graphics, Color color)
    {
        if (_icon is not null)
        {
            EmbeddedIcon.Draw(graphics, _icon, new Point(SpaceLeftOfIcon, (ClientSize.Height - _icon.Height) / 2),
                color);
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
