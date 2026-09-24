using Commands.Parser;
using Commands.Parser.SemanticTree;
using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Parser.Tests;

/// <summary>
/// The core of the grammar, as the original GOLD grammar defined it.
/// </summary>
/// <remarks>
/// These inputs were the equivalence corpus: until decision 0055 removed the GOLD parser,
/// each one was run through both parsers and the two had to agree. They are kept as
/// tests of the one parser that remains. Each accepted input must parse and serialise to
/// a text that parses back to the same serialisation; each rejected one must still be
/// rejected; and the error columns both parsers agreed on are pinned.
/// </remarks>
[TestClass]
public class CoreGrammarTests
{
    private static readonly CommandLineReimagined.Parsing.CommandLineParser Parser = new();

    [TestMethod]
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
    public void TheCoreGrammarParsesAndSerialisesStably(string source)
    {
        string once = Serialise(Parser.Parse<RootNode>(source), source);
        string twice = Serialise(Parser.Parse<RootNode>(once), once);

        Assert.AreEqual(once, twice, $"'{source}' did not serialise to a text that reads back the same.");
    }

    [TestMethod]
    [DataRow("command)")]
    [DataRow("command --flag")]
    [DataRow("echo \"unterminated")]
    [DataRow("|")]
    [DataRow("command |")]
    [DataRow("a | | b")]
    [DataRow("<thing")]
    [DataRow("\"just a string\"")]
    public void MalformedInputIsRejected(string source)
    {
        Assert.IsTrue(Parser.Parse<RootNode>(source).IsT1, $"'{source}' was accepted.");
    }

    [TestMethod]
    [DataRow("command )", 8)]
    [DataRow("command(", 8)]
    [DataRow("a | | b", 4)]
    // `<thing a=1 b` runs out while reading attributes: the error is at the token that
    // cannot be used there, which is what the prompt highlights.
    [DataRow("<thing a=1 b", 11)]
    public void AnErrorIsReportedWhereTheInputWentWrong(string source, int column)
    {
        Assert.AreEqual(column, ErrorColumn(Parser.Parse<RootNode>(source)), $"'{source}' reported another column.");
    }

    private static int ErrorColumn(ParserResult<RootNode> result) =>
        result.Match(
            tree => throw new AssertFailedException("Expected a syntax error."),
            error => error.Match(
                messages => throw new AssertFailedException("Expected a syntax error, not messages."),
                syntax => syntax.Column,
                lexical => lexical.SyntaxError.Column));

    private static string Serialise(ParserResult<RootNode> result, string source) =>
        result.Match(
            tree =>
            {
                var visitor = new SerialisationVisitor();
                tree.Accept(visitor);
                return visitor.GetResult();
            },
            error => throw new AssertFailedException($"'{source}' did not parse."));
}
