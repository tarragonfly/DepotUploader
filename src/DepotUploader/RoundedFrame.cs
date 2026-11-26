namespace DepotUploader;

internal sealed class RoundedFrame : Panel
{
    private readonly Control _content;

    public RoundedFrame(Control content)
    {
        _content = content;
        Dock = DockStyle.Fill;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        content.Dock = DockStyle.None;
        if (WindowTheme.DrawsControls)
        {
            switch (content)
            {
                case TextBoxBase textBox:
                    textBox.BorderStyle = BorderStyle.None;
                    break;
                case ListBox list:
                    list.BorderStyle = BorderStyle.None;
                    break;
            }

            content.BackColorChanged += (_, _) => Invalidate();
        }

        Controls.Add(content);
    }

    protected override void OnLayout(LayoutEventArgs layout)
    {
        base.OnLayout(layout);
        int spaceAroundContent = WindowTheme.DrawsControls ? WindowTheme.SpaceInsideOutline : 0;
        _content.Bounds = Rectangle.Inflate(ClientRectangle, -spaceAroundContent, -spaceAroundContent);
    }

    protected override void OnPaint(PaintEventArgs paint)
    {
        paint.Graphics.Clear(BackColor);
        if (WindowTheme.DrawsControls)
        {
            RoundedBox.Draw(paint.Graphics, ClientRectangle, WindowTheme.CornerRadius, _content.BackColor,
                WindowTheme.Colors.Outline);
        }
    }
}
