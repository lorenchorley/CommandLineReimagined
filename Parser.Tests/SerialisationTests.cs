namespace Parser.Tests;

/// <summary>
/// Round trips: parsing a command and writing it back out should return the original.
/// </summary>
/// <remarks>
/// A round trip is the cheapest evidence that the tree kept everything the text said.
/// Where the output differs, it is because the grammar has two spellings of the same
/// tree and the serialiser picks one, which the normalisation tests below pin down.
/// </remarks>
[TestClass]
public class SerialisationTests
{
    [DataTestMethod]
    // Commands
    [DataRow("")]
    [DataRow("command")]
    [DataRow("command argument")]
    [DataRow("command one two three")]
    [DataRow("command -flag")]
    [DataRow("command -flag value")]
    [DataRow("command -a one -b two")]
    [DataRow("command $variable")]
    [DataRow("echo \"hello world\"")]
    // Function notation
    [DataRow("command()")]
    [DataRow("command(value)")]
    [DataRow("command(name: value)")]
    // Pipes
    [DataRow("first | second")]
    [DataRow("a | b | c")]
    [DataRow("command one | other two")]
    // Tags
    [DataRow("<thing/>")]
    [DataRow("<thing a=1/>")]
    [DataRow("<handle|thing/>")]
    [DataRow("<handle|thing a=1/>")]
    [DataRow("<outer><inner/></outer>")]
    [DataRow("<outer><a/><b/></outer>")]
    [DataRow("<thing label=\"a value\"/>")]
    [DataRow("<thing from=$source/>")]
    [DataRow("<thing/> | command")]
    [DataRow("command | <thing/>")]
    public void RoundTrips(string source) => ParserHarness.AssertRoundTrips(source);

    // Where the grammar allows two spellings, the serialiser emits one of them. These
    // record which, so a rewrite of the parser keeps the same normal form.

    [TestMethod]
    public void EmptyOpenTagNormalisesToTheClosedForm() =>
        Assert.AreEqual("<thing/>", ParserHarness.RoundTrip("<thing></thing>"));

    [TestMethod]
    public void ShorthandClosingTagNormalisesToTheClosedForm() =>
        Assert.AreEqual("<thing/>", ParserHarness.RoundTrip("<thing></>"));

    [TestMethod]
    public void VariableBoundOpenTagNormalisesToTheClosedForm() =>
        Assert.AreEqual("<handle|thing/>", ParserHarness.RoundTrip("<handle|thing></>"));

    [TestMethod]
    public void DoubledQuotesSurviveTheRoundTrip() =>
        Assert.AreEqual("echo \"\"doubled\"\"", ParserHarness.RoundTrip("echo \"\"doubled\"\""));

    [TestMethod]
    public void SeveralAttributesLoseTheirSeparatingSpace()
    {
        // VisitTagAttributes writes the attributes with nothing between them, so
        // `<thing a=1 b=2/>` comes back as `<thing a=1b=2/>`, which no longer reparses
        // to the same tree. A single attribute is unaffected, which is why the round
        // trip cases above pass.
        Assert.AreEqual("<thing a=1b=two/>", ParserHarness.RoundTrip("<thing a=1 b=two/>"));
    }

    [TestMethod]
    public void ReserialisedMultipleAttributesNoLongerParse()
    {
        // The consequence is worse than a changed tree: `a=1b=two` is not valid input at
        // all, so serialising a tag with more than one attribute produces text the
        // parser rejects. Round tripping is not idempotent for these.
        string once = ParserHarness.RoundTrip("<thing a=1 b=two/>");

        Assert.AreEqual("<thing a=1b=two/>", once);
        ParserHarness.ParseError(once);
    }
}
