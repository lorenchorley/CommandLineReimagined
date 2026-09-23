using CommandLineReimagined.Web;
using CommandLineReimagined.Web.Parsing;
using CommandLineReimagined.Web.Tokenisation;

namespace Web.Core.Tests;

/// <summary>
/// How a line that does not parse is described: in phrases, never in grammar labels
/// (Phase 8, finding 29), and with the explanations the grammar gives for a stop with no
/// name after it and an operator with nothing to compare with (findings 27 and 28).
/// </summary>
[TestClass]
public class ParseWordingTests
{
    private TerminalSession _session = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _session = new TerminalSession();
        await _session.InitializeAsync();
    }

    /// <summary>Every phrase the table can produce, which is all a list of expected things may hold.</summary>
    private static readonly HashSet<string> Phrases = new(StringComparer.Ordinal)
    {
        "a command name", "a variable", "a variable name", "an argument", "a flag",
        "a quoted string", "a parenthesised pipeline", "a tag", "a component", "a tag type",
        "an attribute name", "a property name", "a column name", "'not'", "an operator",
        "'try'", "'??'", "'else'", "a pipe", "a comma", "a closing parenthesis",
        "a closing bracket", "the end of the tag", "the end of the component",
        "a closing tag", "the end of the line",
    };

    // ---- the table --------------------------------------------------------------

    [TestMethod]
    [DataRow("identifier", "a command name")]
    [DataRow("$", "a variable")]
    [DataRow("<", "a tag")]
    [DataRow("<$", "a tag")]
    [DataRow("{", "a component")]
    [DataRow("\"", "a quoted string")]
    [DataRow("\"\"", "a quoted string")]
    [DataRow("\"\"\"", "a quoted string")]
    [DataRow("(", "a parenthesised pipeline")]
    [DataRow("argument", "an argument")]
    [DataRow("end of input", "the end of the line")]
    [DataRow("variable name", "a variable name")]
    [DataRow("attribute name", "an attribute name")]
    [DataRow("tag type", "a tag type")]
    [DataRow("-", "a flag")]
    [DataRow("|", "a pipe")]
    [DataRow(")", "a closing parenthesis")]
    [DataRow("eq", "an operator")]
    [DataRow("/>", "the end of the tag")]
    [DataRow("else", "'else'")]
    public void EachLabelReadsAsAPhrase(string label, string phrase) =>
        Assert.AreEqual(phrase, TerminalSession.DescribeExpected(new[] { label }));

    [TestMethod]
    public void LabelsThatReadTheSameAreNamedOnce()
    {
        Assert.AreEqual("a quoted string", TerminalSession.DescribeExpected(new[] { "\"", "\"\"", "\"\"\"" }));
        Assert.AreEqual("a tag", TerminalSession.DescribeExpected(new[] { "<", "<$" }));
        Assert.AreEqual("an operator", TerminalSession.DescribeExpected(new[] { "eq", "gt", "and", "or" }));
    }

    [TestMethod]
    public void TheListIsJoinedWithOr()
    {
        Assert.AreEqual("a tag or a component", TerminalSession.DescribeExpected(new[] { "{", "<" }));
        Assert.AreEqual(
            "a command name, a variable, a parenthesised pipeline or the end of the line",
            TerminalSession.DescribeExpected(new[] { "end of input", "(", "$", "identifier" }));
    }

    /// <summary>A stroke is only expected because a word could go on, which says nothing.</summary>
    [TestMethod]
    public void AStrokeIsLeftOut() =>
        Assert.AreEqual("a comma", TerminalSession.DescribeExpected(new[] { "/", "," }));

    /// <summary>A label the table does not know is quoted as written, never shown bare.</summary>
    [TestMethod]
    public void AnUnknownLabelIsQuoted() =>
        Assert.AreEqual("an argument or ':'", TerminalSession.DescribeExpected(new[] { ":", "argument" }));

    [TestMethod]
    public void ASyntaxErrorSaysWhatWasExpectedInWords()
    {
        var error = new ParseErrorInfo("syntax", 0, 4, new[] { "identifier", "$", "(", "<", "<$", "try", "{" });

        Assert.AreEqual(
            "Syntax error at column 4: expected a command name, a variable, a parenthesised pipeline, a tag, a component or 'try'.",
            TerminalSession.Describe(error));
    }

    [TestMethod]
    public void AnExplanationIsPreferredToTheList()
    {
        var error = new ParseErrorInfo("syntax", 0, 16, new[] { "column name" }, "a column name belongs after the stop, as in $row.kind");

        Assert.AreEqual("Column 16: a column name belongs after the stop, as in $row.kind", TerminalSession.Describe(error));
    }

    // ---- what the page is shown -------------------------------------------------

    /// <summary>Finding 27: the stop is a syntax error, not a path called <c>.</c>.</summary>
    [TestMethod]
    public async Task AStopWithNoNameIsExplained()
    {
        var response = await _session.ExecuteAsync("ls | where $row.");

        Assert.AreEqual("Column 16: a column name belongs after the stop, as in $row.kind", response.Error);
    }

    /// <summary>Finding 28: the operator says what it needs.</summary>
    [TestMethod]
    public async Task AnOperatorWithNothingAfterItIsExplained()
    {
        var response = await _session.ExecuteAsync("ls | where $row.kind eq");

        Assert.AreEqual("Column 23: eq needs a value to compare with, such as folder", response.Error);
    }

    /// <summary>Finding 3: a variable standing alone shows its value.</summary>
    [TestMethod]
    public async Task AVariableStandingAloneShowsItsValue()
    {
        await _session.ExecuteAsync("set v 5");

        var response = await _session.ExecuteAsync("$v");

        Assert.IsNull(response.Error);
        Assert.AreEqual("5", response.Result!.Single().Text);
    }

    /// <summary>Findings 3 and 4: `$row` outside a predicate says where it exists.</summary>
    [TestMethod]
    [DataRow("$row")]
    [DataRow("echo $row")]
    public async Task RowOutsideAPredicateSaysWhereItExists(string line)
    {
        var response = await _session.ExecuteAsync(line);

        Assert.AreEqual(
            "$row is the row a predicate is testing. It exists only inside where, find, cd and save-view: ls | where $row.kind eq folder.",
            response.Error);
    }

    /// <summary>A value stage is coloured as the variable it is.</summary>
    [TestMethod]
    public void AValueStageIsTokenisedAsAVariable()
    {
        var parse = new CommandParseService().Parse("$problem.kind | echo");

        Assert.IsNull(parse.Error);
        Assert.AreEqual("$problem.kind | echo", parse.Reserialised);
        Assert.AreEqual(
            "$problem",
            string.Concat(parse.Tokens.Where(t => t.Kind == TokenStreamVisitor.Kinds.Variable).Select(t => t.Text)));
        Assert.AreEqual(
            ".kind",
            string.Concat(parse.Tokens.Where(t => t.Kind == TokenStreamVisitor.Kinds.Member).Select(t => t.Text)));
    }

    /// <summary>Finding 29: no syntax error the page shows names a grammar label.</summary>
    [TestMethod]
    [DataRow("ls |")]
    [DataRow("|")]
    [DataRow("command)")]
    [DataRow("command(")]
    [DataRow("command(value")]
    [DataRow("command $")]
    [DataRow("echo \"unterminated")]
    [DataRow("a | | b")]
    [DataRow("<thing")]
    [DataRow("</>")]
    [DataRow("<thing broken=/>")]
    [DataRow("\"just a string\"")]
    [DataRow("$v 5")]
    [DataRow("ls )")]
    [DataRow("echo (ls")]
    [DataRow("ls | where not")]
    [DataRow("{c")]
    [DataRow("echo a,")]
    [DataRow("save <note name=x")]
    [DataRow("ls | where $row.kind eq folder and")]
    [DataRow("ls ??")]
    public async Task NoSyntaxErrorNamesAGrammarLabel(string line)
    {
        var response = await _session.ExecuteAsync(line);
        string error = response.Error ?? throw new AssertFailedException($"'{line}' did not fail.");

        const string marker = ": expected ";
        int at = error.IndexOf(marker, StringComparison.Ordinal);

        if (at < 0)
        {
            // An explanation, which is a sentence of the grammar's own.
            StringAssert.StartsWith(error, "Column ");
            return;
        }

        string list = error[(at + marker.Length)..].TrimEnd('.');

        foreach (string phrase in list.Split(new[] { ", ", " or " }, StringSplitOptions.None))
        {
            Assert.IsTrue(Phrases.Contains(phrase), $"'{line}' named '{phrase}': {error}");
        }
    }

    /// <summary>The parse carries the sentence running the line would show.</summary>
    /// <remarks>
    /// So the page's detail line, while typing, says what running the line would say,
    /// rather than keeping its own copy of the wording.
    /// </remarks>
    [TestMethod]
    public void AParseCarriesTheSentenceRunningTheLineWouldShow()
    {
        var error = new CommandParseService().Parse("ls | where $row.").Error!;

        Assert.AreEqual(TerminalSession.Describe(error), error.Sentence);
        StringAssert.Contains(error.Sentence, "a column name belongs after the stop");
    }
}
