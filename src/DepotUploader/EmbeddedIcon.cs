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
}
