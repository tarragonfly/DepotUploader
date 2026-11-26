using System.Drawing.Drawing2D;
using System.Windows.Forms.VisualStyles;

namespace DepotUploader;

internal sealed class ThemeCheckBox : CheckBox
{
    public static void DrawBox(Graphics graphics, Rectangle bounds, bool ticked, bool mouseIsOver, bool enabled,
        Color background)
    {
        ThemeColors colors = WindowTheme.Colors;
        Color fill = ticked ? colors.SelectedFill : colors.CheckBoxFill;
        Color outline = colors.Outline;
        Color checkMark = colors.CheckMark;
        if (!enabled)
        {
            fill = ThemeColors.OfDisabledControl(fill, background);
            outline = ThemeColors.OfDisabledControl(outline, background);
            checkMark = ThemeColors.OfDisabledControl(checkMark, fill);
        }
        else if (mouseIsOver)
        {
            fill = ThemeColors.FillUnderMouse(fill, checkMark);
        }

        RoundedBox.Draw(graphics, bounds, 0.2f * bounds.Height, fill, outline);
        if (!ticked)
        {
            return;
        }

        SmoothingMode smoothingModeBefore = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (Pen checkMarkPen = new Pen(checkMark, Math.Max(1.5f, bounds.Height / 8f))
               {
                   StartCap = LineCap.Round,
                   EndCap = LineCap.Round,
                   LineJoin = LineJoin.Round,
               })
        {
            float width = bounds.Width - 1;
            float height = bounds.Height - 1;
            graphics.DrawLines(checkMarkPen, new[]
            {
                new PointF(bounds.Left + 0.24f * width, bounds.Top + 0.52f * height),
                new PointF(bounds.Left + 0.42f * width, bounds.Top + 0.70f * height),
                new PointF(bounds.Left + 0.76f * width, bounds.Top + 0.30f * height),
            });
        }

        graphics.SmoothingMode = smoothingModeBefore;
    }

    protected override void OnPaint(PaintEventArgs paint)
    {
        if (!WindowTheme.DrawsControls)
        {
            base.OnPaint(paint);

            return;
        }

        Graphics graphics = paint.Graphics;
        graphics.Clear(BackColor);
        Size boxSize = CheckBoxRenderer.GetGlyphSize(graphics, CheckBoxState.UncheckedNormal);
        Rectangle boxBounds = new Rectangle(0, (ClientSize.Height - boxSize.Height) / 2, boxSize.Width,
            boxSize.Height);
        DrawBox(graphics, boxBounds, Checked, ClientRectangle.Contains(PointToClient(MousePosition)), Enabled,
            BackColor);
        Rectangle textBounds = Rectangle.FromLTRB(boxBounds.Right + boxSize.Width / 4, 0, ClientSize.Width,
            ClientSize.Height);
        Color textColor = Enabled ? ForeColor : ThemeColors.OfDisabledControl(ForeColor, BackColor);
        TextRenderer.DrawText(graphics, Text, Font, textBounds, textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        if (Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(graphics, textBounds, textColor, BackColor);
        }
    }
}
