using System.Drawing.Imaging;

namespace DepotUploader;

internal static class EmbeddedIcon
{
    private static readonly int[] PixelSizesLargestFirst = { 32, 24, 20, 16 };

    public static Bitmap LargestThatFits(string name, int availablePixels)
    {
        int pixelSize = PixelSizesLargestFirst.FirstOrDefault(size => size <= availablePixels);
        if (pixelSize == 0)
        {
            pixelSize = PixelSizesLargestFirst.Last();
        }

        using Stream png = typeof(EmbeddedIcon).Assembly.GetManifestResourceStream($"Icons.{name}-{pixelSize}.png")!;
        using Bitmap readFromStream = new Bitmap(png);

        return new Bitmap(readFromStream);
    }

    public static void Draw(Graphics graphics, Bitmap icon, Point topLeft, Color color)
    {
        ColorMatrix everyPixelInColor = new ColorMatrix(new[]
        {
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, color.A / 255f, 0 },
            new float[] { color.R / 255f, color.G / 255f, color.B / 255f, 0, 1 },
        });
        using ImageAttributes recoloring = new ImageAttributes();
        recoloring.SetColorMatrix(everyPixelInColor);
        graphics.DrawImage(icon, new Rectangle(topLeft, icon.Size), 0, 0, icon.Width, icon.Height,
            GraphicsUnit.Pixel, recoloring);
    }

    public static Bitmap OnRoundedFill(Bitmap icon, Color fill, Color iconColor)
    {
        int spaceAroundIcon = icon.Width / 8;
        int pixels = icon.Width + 2 * spaceAroundIcon;
        Bitmap iconOnFill = new Bitmap(pixels, pixels);
        using Graphics graphics = Graphics.FromImage(iconOnFill);
        RoundedBox.Draw(graphics, new Rectangle(0, 0, pixels, pixels), 0.2f * pixels, fill, fill);
        Draw(graphics, icon, new Point(spaceAroundIcon, spaceAroundIcon), iconColor);

        return iconOnFill;
    }
}
