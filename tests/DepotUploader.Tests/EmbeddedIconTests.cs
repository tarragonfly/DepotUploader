namespace DepotUploader.Tests;

public class EmbeddedIconTests
{
    [TestCase(10, 16)]
    [TestCase(16, 16)]
    [TestCase(19, 16)]
    [TestCase(23, 20)]
    [TestCase(24, 24)]
    [TestCase(31, 24)]
    [TestCase(40, 32)]
    public void LargestThatFits_ReturnsTheLargestSizeUpToTheAvailablePixels(int availablePixels, int expectedPixels)
    {
        using Bitmap icon = EmbeddedIcon.LargestThatFits("upload", availablePixels);

        Assert.That(icon.Size, Is.EqualTo(new Size(expectedPixels, expectedPixels)));
    }

    [Test]
    public void LargestThatFits_LoadsEveryIconInEverySize(
        [Values("upload", "stop", "preview", "folder", "trash", "warning", "add", "reload", "valve", "success", "failure",
            "filter", "sort")]
        string name,
        [Values(16, 20, 24, 32)] int pixels)
    {
        using Bitmap icon = EmbeddedIcon.LargestThatFits(name, pixels);

        Assert.That(icon.Size, Is.EqualTo(new Size(pixels, pixels)));
    }

    [Test]
    public void Draw_PaintsEveryPixelOfTheIconInTheGivenColorAndKeepsItsTransparency()
    {
        using Bitmap icon = new Bitmap(2, 1);
        icon.SetPixel(0, 0, Color.FromArgb(255, 10, 200, 30));
        icon.SetPixel(1, 0, Color.FromArgb(0, 255, 255, 255));
        using Bitmap canvas = new Bitmap(2, 1);
        using (Graphics graphics = Graphics.FromImage(canvas))
        {
            EmbeddedIcon.Draw(graphics, icon, Point.Empty, Color.FromArgb(0x47, 0x72, 0xb3));
        }

        Assert.That(canvas.GetPixel(0, 0), Is.EqualTo(Color.FromArgb(255, 0x47, 0x72, 0xb3)));
        Assert.That(canvas.GetPixel(1, 0).A, Is.Zero);
    }

    [Test]
    public void OnRoundedFill_AddsAnEighthOfTheIconSizeOnEverySideAndFillsTheMiddle()
    {
        using Bitmap icon = new Bitmap(16, 16);
        using Bitmap mark = EmbeddedIcon.OnRoundedFill(icon, ThemeColors.WarningFill, Color.White);

        Assert.That(mark.Size, Is.EqualTo(new Size(20, 20)));
        Assert.That(mark.GetPixel(10, 10), Is.EqualTo(ThemeColors.WarningFill));
        Assert.That(mark.GetPixel(0, 0).A, Is.LessThan(128));
    }
}
