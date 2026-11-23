namespace DepotUploader.Tests;

public class JsonTextTests
{
    [Test]
    public void Parse_ReadsObjectsArraysTextNumbersAndWords()
    {
        object? json = JsonText.Parse(
            " { \"text\" : \"a \\\"b\\\" \\\\ \\/ \\u0041\\n\\t\" , \"number\" : -12.5e3 , \"yes\" : true , "
            + "\"no\" : false , \"nothing\" : null , \"list\" : [ 1 , \"two\" , [ ] , { } ] , \"empty\" : { } } \r\n");

        Dictionary<string, object?> members = ((List<KeyValuePair<string, object?>>)json!)
            .ToDictionary(member => member.Key, member => member.Value);
        Assert.That(members["text"], Is.EqualTo("a \"b\" \\ / A\n\t"));
        Assert.That(members["number"], Is.EqualTo(-12500.0));
        Assert.That(members["yes"], Is.EqualTo(true));
        Assert.That(members["no"], Is.EqualTo(false));
        Assert.That(members["nothing"], Is.Null);
        List<object?> list = (List<object?>)members["list"]!;
        Assert.That(list[0], Is.EqualTo(1.0));
        Assert.That(list[1], Is.EqualTo("two"));
        Assert.That(list[2], Is.Empty);
        Assert.That(list[3], Is.Empty);
        Assert.That(members["empty"], Is.Empty);
        Assert.That(members, Has.Count.EqualTo(7));
    }

    [TestCase("null")]
    [TestCase("\"text\"")]
    [TestCase("0")]
    [TestCase("-0.5E+10")]
    [TestCase("1e999")]
    [TestCase("[]")]
    public void Parse_AcceptsAValueThatIsNotAnObject(string json)
    {
        Assert.That(() => JsonText.Parse(json), Throws.Nothing);
    }

    [Test]
    public void Parse_KeepsMembersWithTheSameNameInTheOrderOfTheText()
    {
        List<KeyValuePair<string, object?>> members =
            (List<KeyValuePair<string, object?>>)JsonText.Parse("{\"a\":1,\"b\":\"x\",\"a\":\"2\"}")!;

        Assert.That(members.Select(member => member.Key), Is.EqualTo(new[] { "a", "b", "a" }));
        Assert.That(members.Select(member => member.Value), Is.EqualTo(new object[] { 1.0, "x", "2" }));
    }

    [Test]
    public void Parse_ReadsASurrogatePairWrittenAsTwoEscapes()
    {
        Assert.That(JsonText.Parse("\"\\ud83d\\ude00\""), Is.EqualTo(char.ConvertFromUtf32(0x1F600)));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("{")]
    [TestCase("{\"a\":1")]
    [TestCase("{\"a\":1,")]
    [TestCase("{\"a\":1,}")]
    [TestCase("{\"a\" 1}")]
    [TestCase("{\"a\":}")]
    [TestCase("{a:1}")]
    [TestCase("{'a':1}")]
    [TestCase("{\"a\":'b'}")]
    [TestCase("{\"a\":{\"b\":1}")]
    [TestCase("{\"a\":[1}")]
    [TestCase("[1,]")]
    [TestCase("[1 2]")]
    [TestCase("[1")]
    [TestCase("01")]
    [TestCase("1.")]
    [TestCase("-")]
    [TestCase("+1")]
    [TestCase(".5")]
    [TestCase("1e")]
    [TestCase("tru")]
    [TestCase("nul")]
    [TestCase("True")]
    [TestCase("NaN")]
    [TestCase("\"abc")]
    [TestCase("\"abc\\")]
    [TestCase("\"a\\xb\"")]
    [TestCase("\"a\\u12\"")]
    [TestCase("\"a\\u12g4\"")]
    [TestCase("\"a\tb\"")]
    [TestCase("\"a\\u041\0b\"")]
    [TestCase("\"a\\u+041b\"")]
    [TestCase("\"a\\ud800b\"")]
    [TestCase("\"a\\ude00b\"")]
    [TestCase("\"a\\ude00\\ud83d\"")]
    [TestCase("\"a\\ud83d\"")]
    [TestCase("\"a\" \"b\"")]
    [TestCase("{}{}")]
    [TestCase("{}null")]
    [TestCase("{} x")]
    [TestCase("{}\0")]
    [TestCase("// comment\n{}")]
    [TestCase("{\"a\":1 /* comment */}")]
    public void Parse_RefusesTextThatIsNotOneJsonValue(string json)
    {
        Assert.That(() => JsonText.Parse(json), Throws.InstanceOf<FormatException>());
    }

    [Test]
    public void Parse_RefusesNestingDeeperThan64Levels()
    {
        Assert.That(() => JsonText.Parse(new string('[', 64) + new string(']', 64)), Throws.Nothing);
        Assert.That(() => JsonText.Parse(new string('[', 65) + new string(']', 65)),
            Throws.InstanceOf<FormatException>());
        Assert.That(() => JsonText.Parse(new string('[', 200000)), Throws.InstanceOf<FormatException>());
        Assert.That(() => JsonText.Parse(string.Concat(Enumerable.Repeat("{\"a\":", 200000))),
            Throws.InstanceOf<FormatException>());
    }

    [TestCase("")]
    [TestCase("plain")]
    [TestCase("a \"quoted\" name")]
    [TestCase(@"C:\steamworks\sdk\tools\ContentBuilder\")]
    [TestCase("line one\r\nline two\ttabbed\0zero\u001fend")]
    [TestCase("<tag> & 'apostrophe' /Date(1)/")]
    public void Quote_WritesTextThatParseReadsBack(string text)
    {
        string quoted = JsonText.Quote(text);

        Assert.That(JsonText.Parse(quoted), Is.EqualTo(text));
        Assert.That(quoted, Does.Not.Match("[\\x00-\\x1f]"));
    }
}
