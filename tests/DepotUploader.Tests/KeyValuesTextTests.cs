namespace DepotUploader.Tests;

public class KeyValuesTextTests
{
    [Test]
    public void Parse_KeepsBackslashesInPathsAsWritten()
    {
        KeyValuesNode root = KeyValuesText.Parse("\"appbuild\"\r\n{\r\n\t\"buildoutput\" \"E:\\sdk\\tools\\output\"\r\n}");

        Assert.That(root.Key, Is.EqualTo("appbuild"));
        Assert.That(root.GetValue("buildoutput"), Is.EqualTo("E:\\sdk\\tools\\output"));
    }

    [Test]
    public void Parse_SkipsCommentsAndReadsNestedBlocks()
    {
        KeyValuesNode root = KeyValuesText.Parse(
            "\"AppBuild\"\n{\n\t\"AppID\" \"100030\" // the game\n\t\"Depots\"\n\t{\n\t\t\"100031\" \"windows.vdf\"\n\t}\n}\n");

        Assert.That(root.GetValue("appid"), Is.EqualTo("100030"));
        Assert.That(root.FindChild("depots")!.GetValue("100031"), Is.EqualTo("windows.vdf"));
    }

    [Test]
    public void Parse_KeepsRepeatedKeysInOrder()
    {
        KeyValuesNode root = KeyValuesText.Parse(
            "\"DepotBuildConfig\" { \"FileExclusion\" \"*.pdb\" \"FileExclusion\" \"logs\\*\" }");

        Assert.That(root.Children!.Select(child => child.Value), Is.EqualTo(new[] { "*.pdb", "logs\\*" }));
    }

    [Test]
    public void Write_ThenParse_GivesTheSameKeysAndValues()
    {
        KeyValuesNode root = new KeyValuesNode("appbuild");
        root.SetValue("appid", "100010");
        root.SetValue("desc", "1.1");
        KeyValuesNode depots = new KeyValuesNode("depots");
        depots.SetValue("100011", "E:\\scripts\\depot_100011.vdf");
        root.Children!.Add(depots);

        KeyValuesNode reread = KeyValuesText.Parse(KeyValuesText.Write(root));

        Assert.That(reread.GetValue("appid"), Is.EqualTo("100010"));
        Assert.That(reread.GetValue("desc"), Is.EqualTo("1.1"));
        Assert.That(reread.FindChild("depots")!.GetValue("100011"), Is.EqualTo("E:\\scripts\\depot_100011.vdf"));
    }

    [Test]
    public void Write_RejectsAValueWithADoubleQuote()
    {
        KeyValuesNode root = new KeyValuesNode("appbuild");
        root.SetValue("desc", "the \"final\" build");

        Assert.That(() => KeyValuesText.Write(root), Throws.TypeOf<FormatException>());
    }

    [Test]
    public void Parse_RejectsABlockThatIsNotClosed()
    {
        Assert.That(() => KeyValuesText.Parse("\"appbuild\" { \"appid\" \"1\""), Throws.TypeOf<FormatException>());
    }
}
