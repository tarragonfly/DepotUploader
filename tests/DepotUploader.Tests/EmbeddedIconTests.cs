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
}
