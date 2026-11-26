using System.Drawing.Drawing2D;

namespace DepotUploader;

internal sealed class IconDropDownList : ComboBox
{
    private const int HeightAComboBoxAddsToItsItems = 6;

    private readonly Bitmap _icon;

    public IconDropDownList(string iconName)
    {
        _icon = EmbeddedIcon.LargestThatFits(iconName, WindowTheme.LineHeight);
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = WindowTheme.ControlHeight - HeightAComboBoxAddsToItsItems;
        if (WindowTheme.DrawsControls)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
    }

    protected override void OnPaint(PaintEventArgs paint)
    {
        ThemeColors colors = WindowTheme.Colors;
        Color background = Parent?.BackColor ?? colors.WindowFill;
        Color fill = DroppedDown ? colors.OpenDropDownFill : colors.DropDownFill;
        Color outline = Focused && ShowFocusCues ? colors.DropDownText : colors.Outline;
        if (!DroppedDown && ClientRectangle.Contains(PointToClient(MousePosition)))
        {
            fill = ThemeColors.FillUnderMouse(fill, colors.DropDownText);
        }

        Graphics graphics = paint.Graphics;
        graphics.Clear(background);
        RoundedBox.Draw(graphics, ClientRectangle, WindowTheme.CornerRadius, fill, outline);
        int space = WindowTheme.SpaceInsideOutline;
        EmbeddedIcon.Draw(graphics, _icon, new Point(space, (ClientSize.Height - _icon.Height) / 2),
            colors.DropDownText);
        int arrowWidth = WindowTheme.LineHeight / 2;
        Rectangle textBounds = Rectangle.FromLTRB(2 * space + _icon.Width, 0,
            ClientSize.Width - 2 * space - arrowWidth, ClientSize.Height);
        TextRenderer.DrawText(graphics, GetItemText(SelectedItem), Font, textBounds, colors.DropDownText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix);
        DrawArrow(graphics, DroppedDown ? colors.DropDownText : colors.DropDownArrow,
            ClientSize.Width - space - arrowWidth, arrowWidth);
    }

    private void DrawArrow(Graphics graphics, Color color, int left, int width)
    {
        float middle = (ClientSize.Height - 1) / 2f;
        float height = width / 2f;
        SmoothingMode smoothingModeBefore = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (Pen arrowPen = new Pen(color, Math.Max(1.5f, WindowTheme.LineHeight / 10f))
               {
                   StartCap = LineCap.Round,
                   EndCap = LineCap.Round,
                   LineJoin = LineJoin.Round,
               })
        {
            graphics.DrawLines(arrowPen, new[]
            {
                new PointF(left, middle - height / 2),
                new PointF(left + width / 2f, middle + height / 2),
                new PointF(left + width, middle - height / 2),
            });
        }

        graphics.SmoothingMode = smoothingModeBefore;
    }

    protected override void OnDrawItem(DrawItemEventArgs item)
    {
        if (item.Index < 0)
        {
            return;
        }

        bool isSelected = (item.State & DrawItemState.Selected) != 0;
        Color fill = item.BackColor;
        Color textColor = item.ForeColor;
        if (WindowTheme.DrawsControls)
        {
            fill = isSelected ? WindowTheme.Colors.SelectedFill : WindowTheme.Colors.DropDownItemFill;
            textColor = isSelected ? WindowTheme.Colors.SelectedText : WindowTheme.Colors.DropDownItemText;
        }

        using (SolidBrush fillBrush = new SolidBrush(fill))
        {
            item.Graphics.FillRectangle(fillBrush, item.Bounds);
        }

        int left = item.Bounds.Left + WindowTheme.SpaceInsideOutline;
        if ((item.State & DrawItemState.ComboBoxEdit) != 0)
        {
            EmbeddedIcon.Draw(item.Graphics, _icon,
                new Point(left, item.Bounds.Top + (item.Bounds.Height - _icon.Height) / 2), textColor);
            left += _icon.Width + WindowTheme.SpaceInsideOutline;
        }

        TextRenderer.DrawText(item.Graphics, GetItemText(Items[item.Index]), item.Font,
            Rectangle.FromLTRB(left, item.Bounds.Top, item.Bounds.Right, item.Bounds.Bottom), textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (!WindowTheme.DrawsControls)
        {
            item.DrawFocusRectangle();
        }
    }

    protected override void OnMouseEnter(EventArgs mouse)
    {
        base.OnMouseEnter(mouse);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs mouse)
    {
        base.OnMouseLeave(mouse);
        Invalidate();
    }

    protected override void OnDropDown(EventArgs dropDown)
    {
        base.OnDropDown(dropDown);
        Invalidate();
    }

    protected override void OnDropDownClosed(EventArgs dropDown)
    {
        base.OnDropDownClosed(dropDown);
        Invalidate();
    }

    protected override void OnSelectedIndexChanged(EventArgs selection)
    {
        base.OnSelectedIndexChanged(selection);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _icon.Dispose();
        }

        base.Dispose(disposing);
    }
}
