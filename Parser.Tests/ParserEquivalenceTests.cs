using Commands.Parser;
using Commands.Parser.SemanticTree;
using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Parser.Tests;

/// <summary>
/// Runs the GOLD interpreter and the FParsec parser over the same inputs and compares
/// what they produce.
/// </summary>
/// <remarks>
/// Evidence that the migration preserved the language rather than quietly redefining it.
/// The corpus is restricted to grammar the old parser could actually build, since
/// everything it threw on is covered by CompletedGrammarTests instead.
///
/// It is also restricted to inputs whose meaning the language has not changed since.
/// The grammar grew after the migration — bare words in attributes, word operators and
/// member access, reserved words, <c>else</c>, <c>try</c>, <c>??</c>, pipelines in
/// parentheses, hyphenated command names — and the GOLD grammar, which is frozen, reads
/// some of those inputs differently or not at all. Those cases live in the FParsec-only
/// classes (BareWordTests, ExpressionTests, RecoveryTests, CommandFormTests), and a new
/// case belongs there rather than here.
/// </remarks>
[TestClass]
public class ParserEquivalenceTests
{
    private static readonly CommandLineInterpreter Gold = new();
    private static readonly CommandLineReimagined.Parsing.CommandLineParser FParsec = new();

    [DataTestMethod]
    [DataRow("")]
    [DataRow("command")]
    [DataRow("command argument")]
    [DataRow("command one two three")]
    [DataRow("command -flag")]
    [DataRow("command -flag value")]
    [DataRow("command -a one -b two")]
    [DataRow("command $variable")]
    [DataRow("café")]
    [DataRow("my_command 42")]
    [DataRow("echo \"hello world\"")]
    [DataRow("echo \"\"doubled\"\"")]
    [DataRow("command()")]
    [DataRow("command(value)")]
    [DataRow("command(name: value)")]
    [DataRow("first | second")]
    [DataRow("a | b | c")]
    [DataRow("command one | other two")]
    [DataRow("<thing/>")]
    [DataRow("<thing a=1/>")]
    [DataRow("<handle|thing/>")]
    [DataRow("<handle|thing a=1/>")]
    [DataRow("<thing></thing>")]
    [DataRow("<thing></>")]
    [DataRow("<outer><inner/></outer>")]
    [DataRow("<outer><a/><b/><c/></outer>")]
    [DataRow("<a><b><c/></b></a>")]
    [DataRow("<thing label=\"a value\"/>")]
    [DataRow("<thing from=$source/>")]
    [DataRow("<thing/> | command")]
    [DataRow("command | <thing/>")]
    [DataRow("<t>[size=3]</t>")]
    public void BothParsersProduceTheSameTree(string source)
    {
        string gold = SerialiseWith(Gold.Parse<RootNode>(source), source, "GOLD");
        string fparsec = SerialiseWith(FParsec.Parse<RootNode>(source), source, "FParsec");

        Assert.AreEqual(gold, fparsec, $"The two parsers disagree on '{source}'.");
    }

    [DataTestMethod]
    [DataRow("command)")]
    [DataRow("command --flag")]
    [DataRow("echo \"unterminated")]
    [DataRow("|")]
    [DataRow("command |")]
    [DataRow("a | | b")]
    [DataRow("<thing")]
    [DataRow("\"just a string\"")]
    public void BothParsersRejectTheSameInput(string source)
    {
        Assert.IsTrue(Gold.Parse<RootNode>(source).IsT1, $"GOLD accepted '{source}'.");
        Assert.IsTrue(FParsec.Parse<RootNode>(source).IsT1, $"FParsec accepted '{source}'.");
    }

    [DataTestMethod]
    [DataRow("command )", 8)]
    [DataRow("command(", 8)]
    [DataRow("a | | b", 4)]
    public void BothParsersAgreeOnWhereTheErrorIs(string source, int column)
    {
        Assert.AreEqual(column, ErrorColumn(Gold.Parse<RootNode>(source)), "GOLD reported a different column.");
        Assert.AreEqual(column, ErrorColumn(FParsec.Parse<RootNode>(source)), "FParsec reported a different column.");
    }

    [TestMethod]
    public void TruncatedInputIsReportedAtTheTokenRatherThanAtTheEnd()
    {
        // `<thing a=1 b` runs out while reading attributes. The two parsers point at
        // different places and both are defensible: GOLD blames end of input, FParsec
        // blames the token that cannot be used there. The prompt highlights the column,
        // so pointing at `b` is the more useful of the two.
        const string source = "<thing a=1 b";

        Assert.AreEqual(12, ErrorColumn(Gold.Parse<RootNode>(source)), "GOLD should blame end of input.");
        Assert.AreEqual(11, ErrorColumn(FParsec.Parse<RootNode>(source)), "FParsec should blame the token.");
    }

    private static int ErrorColumn(ParserResult<RootNode> result) =>
        result.Match(
            tree => throw new AssertFailedException("Expected a syntax error."),
            error => error.Match(
                messages => throw new AssertFailedException("Expected a syntax error, not messages."),
                syntax => syntax.Column,
                lexical => lexical.SyntaxError.Column));

    private static string SerialiseWith(ParserResult<RootNode> result, string source, string parser) =>
        result.Match(
            tree =>
            {
                var visitor = new SerialisationVisitor();
                tree.Accept(visitor);
                return visitor.GetResult();
            },
            error => throw new AssertFailedException($"{parser} did not parse '{source}'."));
}
