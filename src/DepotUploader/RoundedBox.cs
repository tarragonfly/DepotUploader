using System.Drawing.Drawing2D;

namespace DepotUploader;

internal static class RoundedBox
{
    public static void Draw(Graphics graphics, Rectangle bounds, float cornerRadius, Color fill, Color outline)
    {
        if (bounds.Width < 2 || bounds.Height < 2)
        {
            return;
        }

        SmoothingMode smoothingModeBefore = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath edge = EdgeOf(bounds, cornerRadius))
        using (SolidBrush fillBrush = new SolidBrush(fill))
        using (Pen outlinePen = new Pen(outline))
        {
            graphics.FillPath(fillBrush, edge);
            graphics.DrawPath(outlinePen, edge);
        }

        graphics.SmoothingMode = smoothingModeBefore;
    }

    private static GraphicsPath EdgeOf(Rectangle bounds, float cornerRadius)
    {
        float width = bounds.Width - 1;
        float height = bounds.Height - 1;
        float diameter = Math.Max(1f, Math.Min(2 * cornerRadius, Math.Min(width, height)));
        float right = bounds.Left + width - diameter;
        float bottom = bounds.Top + height - diameter;
        GraphicsPath edge = new GraphicsPath();
        edge.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        edge.AddArc(right, bounds.Top, diameter, diameter, 270, 90);
        edge.AddArc(right, bottom, diameter, diameter, 0, 90);
        edge.AddArc(bounds.Left, bottom, diameter, diameter, 90, 90);
        edge.CloseFigure();

        return edge;
    }
}
