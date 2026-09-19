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
    [DataTestMethod]
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
    [DataRow("$variable")]
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
    public void TripleQuotedStringIsRejected()
    {
        // StringLiteral4 ('"""' ... '"""') is defined in the grammar but <Constant> lists
        // StringLiteral3 twice and never refers to StringLiteral4, so the three-quote
        // form cannot be used. StringConstant's own trimming handles any quote count,
        // so this is a defect in the grammar rather than an intended restriction.
        ParserHarness.ParseError("echo \"\"\"triple\"\"\"");
    }
}
