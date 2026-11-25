namespace DepotUploader.Tests;

public class BatchResultLineTests
{
    private static readonly DateTime EndTime = new DateTime(2026, 11, 25, 14, 2, 0);

    [Test]
    public void Text_CountsTheGamesWhenEveryGameFinished()
    {
        string text = BatchResultLine.Text(new[]
        {
            ("Neon Garden", UploadOutcome.Finished),
            ("Paper Kingdom", UploadOutcome.Finished),
            ("Zinc Lantern", UploadOutcome.Finished),
        }, EndTime);

        Assert.That(text, Is.EqualTo("3 of 3 games finished at 14:02"));
    }

    [Test]
    public void Text_NamesEveryFailedGame()
    {
        string text = BatchResultLine.Text(new[]
        {
            ("Neon Garden", UploadOutcome.Failed),
            ("Paper Kingdom", UploadOutcome.Finished),
            ("Zinc Lantern", UploadOutcome.Failed),
        }, EndTime);

        Assert.That(text, Is.EqualTo("1 of 3 games finished at 14:02. Failed: Neon Garden, Zinc Lantern"));
    }

    [Test]
    public void Text_NamesTheStoppedGameAndTheGamesThatWereNotStarted()
    {
        string text = BatchResultLine.Text(new[]
        {
            ("Neon Garden", UploadOutcome.Finished),
            ("Paper Kingdom", UploadOutcome.Stopped),
            ("Zinc Lantern", UploadOutcome.NotStarted),
            ("Amber Circuit", UploadOutcome.NotStarted),
        }, EndTime);

        Assert.That(text, Is.EqualTo(
            "1 of 4 games finished at 14:02. Stopped: Paper Kingdom. Not started: Zinc Lantern, Amber Circuit"));
    }

    [Test]
    public void Text_ListsFailedBeforeStoppedBeforeNotStarted()
    {
        string text = BatchResultLine.Text(new[]
        {
            ("Neon Garden", UploadOutcome.Failed),
            ("Paper Kingdom", UploadOutcome.Stopped),
            ("Zinc Lantern", UploadOutcome.NotStarted),
        }, EndTime);

        Assert.That(text, Is.EqualTo(
            "0 of 3 games finished at 14:02. Failed: Neon Garden. Stopped: Paper Kingdom. Not started: Zinc Lantern"));
    }
}
