namespace Parser.Tests;

/// <summary>
/// What the parser rejects, and where it says the problem is.
/// </summary>
/// <remarks>
/// Positions matter: the prompt uses them to mark the offending text, so they are part
/// of the parser's contract and not an implementation detail.
/// </remarks>
[TestClass]
public class SyntaxErrorTests
{
    [TestMethod]
    [DataRow("command)")]
    [DataRow("command(")]
    [DataRow("command(value")]
    [DataRow("command --flag")]
    [DataRow("command -")]
    [DataRow("command $")]
    [DataRow("echo \"unterminated")]
    [DataRow("|")]
    [DataRow("| command")]
    [DataRow("command |")]
    [DataRow("a | | b")]
    [DataRow("<thing")]
    [DataRow("<thing/")]
    [DataRow("</>")]
    [DataRow("<thing broken=/>")]
    [DataRow("<|thing/>")]
    [DataRow("\"just a string\"")]
    public void IsRejected(string source) => ParserHarness.ParseError(source);

    [TestMethod]
    public void ErrorCarriesAColumn()
    {
        var error = ParserHarness.ParseError("command )");

        Assert.IsTrue(error.Column > 0, "The error should point past the start of the line.");
    }

    [TestMethod]
    public void ErrorPointsAtTheOffendingToken()
    {
        // The stray parenthesis is at index 8.
        var error = ParserHarness.ParseError("command )");

        Assert.AreEqual(8, error.Column);
    }

    [TestMethod]
    public void ErrorListsWhatWasExpected()
    {
        var error = ParserHarness.ParseError("command )");

        Assert.IsNotNull(error.ExpectedSymbols);
        Assert.IsTrue(error.ExpectedSymbols.Count() > 0, "The parser should say what it wanted.");
    }

    [TestMethod]
    public void UnbalancedQuoteCountIsRejected()
    {
        // The delimiters have to match: three to open and two to close is not a literal.
        ParserHarness.ParseError("echo \"\"\"mismatched\"\"");
    }
}
