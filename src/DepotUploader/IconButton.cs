using System.Windows.Forms.VisualStyles;

namespace DepotUploader;

internal sealed class IconButton : Button
{
    private static readonly int IconPixels = SystemFonts.MessageBoxFont.Height;
    private static readonly int SpaceLeftOfIcon = IconPixels / 3;
    private static readonly int SpaceBesideText = SpaceLeftOfIcon + IconPixels + IconPixels / 4;
    private static readonly int ButtonHeight = IconPixels + 2 * (IconPixels * 3 / 8);

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
        return new Size(TextRenderer.MeasureText(Text, Font).Width + 2 * SpaceBesideText, ButtonHeight);
    }

    protected override void OnPaint(PaintEventArgs paint)
    {
        if (DarkMode.IsOn)
        {
            PaintDarkButtonWithText(paint.Graphics);
        }
        else
        {
            base.OnPaint(paint);
        }

        if (_icon is null)
        {
            return;
        }

        int top = (ClientSize.Height - _icon.Height) / 2;
        if (Enabled)
        {
            paint.Graphics.DrawImage(_icon, new Rectangle(SpaceLeftOfIcon, top, _icon.Width, _icon.Height));
        }
        else
        {
            ControlPaint.DrawImageDisabled(paint.Graphics, _icon, SpaceLeftOfIcon, top, BackColor);
        }
    }

    private void PaintDarkButtonWithText(Graphics graphics)
    {
        graphics.Clear(BackColor);
        DarkMode.DrawPushButton(graphics, ClientRectangle, DarkButtonState());
        TextRenderer.DrawText(graphics, Text, Font, ClientRectangle,
            Enabled ? DarkMode.TextColor : DarkMode.DisabledTextColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(ClientRectangle, -3, -3),
                DarkMode.TextColor, BackColor);
        }
    }

    private PushButtonState DarkButtonState()
    {
        if (!Enabled)
        {
            return PushButtonState.Disabled;
        }

        if (ClientRectangle.Contains(PointToClient(MousePosition)))
        {
            return Capture ? PushButtonState.Pressed : PushButtonState.Hot;
        }

        return Focused || IsDefault ? PushButtonState.Default : PushButtonState.Normal;
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
