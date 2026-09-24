using System.Linq;
using Commands.Parser.SemanticTree;

namespace Parser.Tests;

/// <summary>
/// Bare words: unquoted command arguments that are not plain identifiers, such as file
/// names with a dot and paths with separators.
/// </summary>
/// <remarks>
/// An extension over the GOLD grammar, which sketched a {BareStringCharacter} set and
/// never used it. Tag attributes take them too, since decision 0007 made `/>` a
/// delimiter recognised by lookahead rather than `/` a delimiter on its own.
/// </remarks>
[TestClass]
public class BareWordTests
{
    [TestMethod]
    [DataRow("read notes.txt", "notes.txt")]
    [DataRow("in ..", "..")]
    [DataRow("in ../documents", "../documents")]
    [DataRow("in /home/terminal", "/home/terminal")]
    [DataRow(@"in C:\Users\me", @"C:\Users\me")]
    [DataRow("in ~/projects", "~/projects")]
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
        var function = ParserHarness.Function("read(path: documents/notes.txt)");
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
        var pipeline = (PipedCommandList)ParserHarness.Parse("read notes.txt|echo");

        Assert.AreEqual(2, pipeline.OrderedCommands.Count);
    }

    [TestMethod]
    public void TheCommandNameItselfIsStillAnIdentifier()
    {
        // The name stops at the dot; what follows starts a word, since `in ..` needs a
        // leading dot to be one. So this is `now` with two arguments, not a syntax error.
        // (It was `not` until Phase 5, when a reserved word stopped being able to name a
        // command at all.)
        var cli = ParserHarness.Cli("now.a.command arg");

        Assert.AreEqual("now", cli.Name.Name);
        Assert.AreEqual(2, cli.Arguments.Arguments.Count);
        Assert.AreEqual(".a.command", ((Identifier)((CommandArgumentValue)cli.Arguments.Arguments[0]).Value).Name);
    }

    // ------------------------------------------------- Decision 0007, part B

    /// <summary>
    /// A tag attribute takes a bare word, and a path inside one keeps its slashes.
    /// </summary>
    /// <remarks>
    /// This reverses what the grammar used to do. A `/` was a delimiter wherever it
    /// appeared, so `path=documents/notes.txt` had to be quoted and the test here
    /// asserted the syntax error. A `/` is now part of the word unless a `>` or a `}`
    /// follows it, so the last one closes the tag and the rest belong to the path.
    /// </remarks>
    [TestMethod]
    [DataRow("<file path=documents/notes.txt/>", "documents/notes.txt")]
    [DataRow("<t path=a/>", "a")]
    [DataRow("<t path=/absolute/path/>", "/absolute/path")]
    [DataRow("<t path=../up/>", "../up")]
    [DataRow("<t name=b/>", "b")]
    public void TagAttributeValuesTakeBareWords(string source, string expected)
    {
        var instance = ParserHarness.Instance(source);

        Assert.AreEqual(1, instance.Attributes!.Attributes.Count);
        Assert.AreEqual(expected, ((Identifier)instance.Attributes.Attributes[0].Value).Name);
        Assert.IsFalse(instance.HasChildren, $"'{source}' should have closed itself.");
    }

    /// <summary>A component tag closes on `/}`, so a word inside one stops there too.</summary>
    [TestMethod]
    public void ComponentAttributeValuesStopBeforeTheClosingBrace()
    {
        var component = (ComponentInstance)ParserHarness.SingleCommand("{c path=a/b/}").Expression.AsT2;

        Assert.AreEqual("a/b", ((Identifier)component.Attributes!.Attributes[0].Value).Name);
        Assert.IsFalse(component.HasChildren);
    }

    /// <summary>A `-` in front of a digit is a number, not a flag.</summary>
    [TestMethod]
    [DataRow("echo -5", "-5")]
    [DataRow("echo -5.5", "-5.5")]
    [DataRow("echo -0", "-0")]
    public void ADashBeforeADigitStartsAWord(string source, string expected)
    {
        var arguments = ParserHarness.Cli(source).Arguments.Arguments;

        Assert.AreEqual(1, arguments.Count);
        Assert.IsInstanceOfType(arguments[0], typeof(CommandArgumentValue),
            $"'{source}' should be a value, not a flag.");
        Assert.AreEqual(expected, ((Identifier)((CommandArgumentValue)arguments[0]).Value).Name);
    }

    /// <summary>A `-` in front of anything else is still a flag.</summary>
    [TestMethod]
    public void ADashBeforeALetterIsStillAFlag()
    {
        var arguments = ParserHarness.Cli("echo -x").Arguments.Arguments;

        Assert.IsInstanceOfType(arguments[0], typeof(CommandArgumentFlag));
        Assert.AreEqual("x", ((CommandArgumentFlag)arguments[0]).Name);
    }

    /// <summary>`name=value` with no spaces is an assignment: a name carrying data.</summary>
    [TestMethod]
    public void AssignmentsAreSeparateFromValues()
    {
        var arguments = ParserHarness.Cli("attr notes.txt tag=work due=2026-10-01").Arguments.Arguments;

        Assert.AreEqual(3, arguments.Count);
        Assert.AreEqual("notes.txt", ((Identifier)((CommandArgumentValue)arguments[0]).Value).Name);

        var tag = (AssignmentArgument)arguments[1];
        Assert.AreEqual("tag", tag.Name.Name);
        Assert.AreEqual("work", ((Identifier)tag.Value).Name);

        var due = (AssignmentArgument)arguments[2];
        Assert.AreEqual("due", due.Name.Name);
        Assert.AreEqual("2026-10-01", ((Identifier)due.Value).Name);
    }

    /// <summary>An assignment's value can be a quoted string, a variable or a tag.</summary>
    [TestMethod]
    [DataRow("attr f note=\"two words\"")]
    [DataRow("attr f owner=$me")]
    [DataRow("attr f size=<dimension value=3/>")]
    public void AssignmentValuesAreOrdinaryValues(string source)
    {
        var arguments = ParserHarness.Cli(source).Arguments.Arguments;

        Assert.IsInstanceOfType(arguments[1], typeof(AssignmentArgument));
    }

    /// <summary>
    /// The `=` has to be tight against both sides, so spacing is what tells an
    /// assignment from two arguments that happen to sit either side of an equals sign.
    /// </summary>
    /// <remarks>
    /// A spaced `=` is a syntax error rather than three words, because `=` is not a
    /// word character and never has been: there is no reading of `echo a = b` that the
    /// grammar accepts. What matters for decision 0017 is that it is not an
    /// assignment, which is what this pins down; giving `=` a meaning of its own as a
    /// bare word is a separate question nobody has asked for.
    /// </remarks>
    [TestMethod]
    [DataRow("echo a = b")]
    [DataRow("echo a =b")]
    [DataRow("echo a= b")]
    public void ASpacedEqualsIsNotAnAssignment(string source) =>
        ParserHarness.ParseError(source);

    [TestMethod]
    public void ATightEqualsIs()
    {
        var arguments = ParserHarness.Cli("echo a=b").Arguments.Arguments;

        Assert.AreEqual(1, arguments.Count);
        Assert.IsInstanceOfType(arguments[0], typeof(AssignmentArgument));
    }

    /// <summary>
    /// `name: value` still binds a declared parameter, so the two notations stay
    /// distinct (decision 0017).
    /// </summary>
    [TestMethod]
    public void AColonStillBindsAParameter()
    {
        var function = ParserHarness.Function("read(path: notes.txt)");

        Assert.IsInstanceOfType(function.Arguments.Arguments[0], typeof(OptionalCommandArgument));
    }

    /// <summary>
    /// `<` opens a tag only when a name, a `$` or a `/` follows it directly. This is
    /// what leaves the bracket free to mean less-than in Phase 3.
    /// </summary>
    [TestMethod]
    [DataRow("< thing")]
    [DataRow("echo < thing")]
    public void ASpacedBracketDoesNotOpenATag(string source) =>
        ParserHarness.ParseError(source);

    [TestMethod]
    [DataRow("<thing/>")]
    [DataRow("<$name>")]
    [DataRow("<thing><inner/></thing>")]
    public void ATightBracketStillOpensATag(string source) =>
        ParserHarness.Parse(source);

    [TestMethod]
    [DataRow("read documents/notes.txt")]
    [DataRow("<file path=documents/notes.txt/>")]
    [DataRow("<t path=a/>")]
    [DataRow("echo -5")]
    [DataRow("attr notes.txt tag=work due=2026-10-01")]
    [DataRow("attr f owner=$me")]
    [DataRow("{c path=a/b/}")]
    public void BareWordsRoundTripThroughTheSerialiser(string source) =>
        ParserHarness.AssertRoundTrips(source);
}
