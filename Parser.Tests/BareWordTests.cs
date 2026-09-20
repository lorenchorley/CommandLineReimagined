using Commands.Parser.SemanticTree;

namespace Parser.Tests;

/// <summary>
/// Bare words: unquoted command arguments that are not plain identifiers, such as file
/// names with a dot and paths with separators.
/// </summary>
/// <remarks>
/// An extension over the GOLD grammar, which sketched a {BareStringCharacter} set and
/// never used it. Only command-argument positions accept them; a tag attribute's value
/// does not, because there `/` closes the tag.
/// </remarks>
[TestClass]
public class BareWordTests
{
    [DataTestMethod]
    [DataRow("cat notes.txt", "notes.txt")]
    [DataRow("cd ..", "..")]
    [DataRow("cd ../documents", "../documents")]
    [DataRow("cd /home/terminal", "/home/terminal")]
    [DataRow(@"cd C:\Users\me", @"C:\Users\me")]
    [DataRow("cd ~/projects", "~/projects")]
    [DataRow("download https://example.com/file.tar.gz", "https://example.com/file.tar.gz")]
    [DataRow("echo not-a-url", "not-a-url")]
    [DataRow("echo user@host", "user@host")]
    public void BareWordsAreAcceptedAsCliArguments(string source, string expected)
    {
        var argument = (CommandArgumentValue)ParserHarness.Cli(source).Arguments.Arguments[0];

        Assert.AreEqual(expected, ((Identifier)argument.Value).Name);
    }

    [TestMethod]
    public void BareWordsAreAcceptedAsFunctionArguments()
    {
        var function = ParserHarness.Function("write(notes.txt, hello)");
        var first = (RequiredCommandArgument)function.Arguments.Arguments[0];

        Assert.AreEqual("notes.txt", ((Identifier)first.Value).Name);
        Assert.AreEqual(2, function.Arguments.Arguments.Count);
    }

    [TestMethod]
    public void BareWordsAreAcceptedAsNamedFunctionArguments()
    {
        var function = ParserHarness.Function("cat(path: documents/notes.txt)");
        var argument = (OptionalCommandArgument)function.Arguments.Arguments[0];

        Assert.AreEqual("documents/notes.txt", ((Identifier)argument.Value).Name);
    }

    [TestMethod]
    public void ADashStillStartsAFlag()
    {
        var arguments = ParserHarness.Cli("ls -path documents").Arguments.Arguments;

        Assert.IsInstanceOfType(arguments[0], typeof(CommandArgumentFlag));
        Assert.AreEqual("path", ((CommandArgumentFlag)arguments[0]).Name);
    }

    [TestMethod]
    public void ADashInsideAWordIsPartOfIt()
    {
        var arguments = ParserHarness.Cli("echo well-known").Arguments.Arguments;

        Assert.AreEqual(1, arguments.Count);
        Assert.AreEqual("well-known", ((Identifier)((CommandArgumentValue)arguments[0]).Value).Name);
    }

    [TestMethod]
    public void APipeEndsAWord()
    {
        var pipeline = (PipedCommandList)ParserHarness.Parse("cat notes.txt|echo");

        Assert.AreEqual(2, pipeline.OrderedCommands.Count);
    }

    [TestMethod]
    public void TheCommandNameItselfIsStillAnIdentifier()
    {
        // The name stops at the dot; what follows starts a word, since `cd ..` needs a
        // leading dot to be one. So this is `not` with two arguments, not a syntax error.
        var cli = ParserHarness.Cli("not.a.command arg");

        Assert.AreEqual("not", cli.Name.Name);
        Assert.AreEqual(2, cli.Arguments.Arguments.Count);
        Assert.AreEqual(".a.command", ((Identifier)((CommandArgumentValue)cli.Arguments.Arguments[0]).Value).Name);
    }

    [TestMethod]
    public void TagAttributeValuesDoNotTakeBareWords()
    {
        // `<thing path=a/b/>` would otherwise swallow the closing `/`.
        var instance = ParserHarness.Instance("<thing name=b/>");

        Assert.AreEqual("b", ((Identifier)instance.Attributes!.Attributes[0].Value).Name);
        ParserHarness.ParseError("<thing path=a/b/>");
    }

    [TestMethod]
    public void BareWordsRoundTripThroughTheSerialiser() =>
        ParserHarness.AssertRoundTrips("cat documents/notes.txt");
}
