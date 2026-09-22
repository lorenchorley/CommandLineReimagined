using Commands.Parser.SemanticTree;

namespace Parser.Tests;

/// <summary>
/// The terminal rules: Identifier, FlagIdentifier, the string literals and the variable
/// sigil, as declared in CommandLineGrammar.grm.
/// </summary>
[TestClass]
public class LexicalTests
{
    // Identifier = {IdentifierCharacter}+ where IdentifierCharacter is AlphaNumeric
    // plus underscore and a long list of accented letters.

    [DataTestMethod]
    [DataRow("command")]
    [DataRow("Command")]
    [DataRow("cmd123")]
    [DataRow("my_command")]
    [DataRow("_leading")]
    [DataRow("trailing_")]
    public void IdentifiersAreAccepted(string identifier) =>
        Assert.AreEqual(identifier, ParserHarness.Cli(identifier).Name.Name);

    [DataTestMethod]
    [DataRow("café")]
    [DataRow("naïve")]
    [DataRow("größe")]
    [DataRow("piñata")]
    [DataRow("Ærø")]
    public void AccentedIdentifiersAreAccepted(string identifier) =>
        Assert.AreEqual(identifier, ParserHarness.Cli(identifier).Name.Name);

    [TestMethod]
    public void DigitsAloneAreAnIdentifier()
    {
        // NumberLiteral is commented out in the grammar, so a bare number lexes as an
        // Identifier. Numeric interpretation happens later, when arguments are bound.
        Assert.AreEqual("42", ParserHarness.Cli("42").Name.Name);
    }

    [TestMethod]
    public void GrammarIsCaseInsensitive()
    {
        // "Case Sensitive" = False in the grammar. Casing is preserved in the tree even
        // though the grammar ignores it when matching.
        Assert.AreEqual("MiXeDcAsE", ParserHarness.Cli("MiXeDcAsE").Name.Name);
    }

    // FlagIdentifier = '-' {IdentifierCharacter}+

    [TestMethod]
    public void FlagKeepsItsNameWithoutTheDash()
    {
        var flag = (CommandArgumentFlag)ParserHarness.Cli("command -verbose").Arguments.Arguments[0];

        Assert.AreEqual("verbose", flag.Name);
    }

    [TestMethod]
    public void FlagRequiresAtLeastOneCharacter() => ParserHarness.ParseError("command -");

    [TestMethod]
    public void DoubleDashIsNotAFlag()
    {
        // FlagIdentifier takes a single '-', so '--flag' does not lex.
        ParserHarness.ParseError("command --flag");
    }

    // StringLiteral2 = '"' {StringCharacter}* '"'

    [TestMethod]
    public void SingleQuotedStringIsAConstant()
    {
        var value = ValueOfFirstArgument("echo \"hello world\"");

        Assert.AreEqual("hello world", ((StringConstant)value).Value);
    }

    [TestMethod]
    public void EmptyStringIsAConstant() =>
        Assert.AreEqual(string.Empty, ((StringConstant)ValueOfFirstArgument("echo \"\"")).Value);

    /// <summary>`""""` is the empty string written with doubled quotes.</summary>
    /// <remarks>
    /// Delimiters are matched longest first, so this is one doubled string with nothing in
    /// it, and every delimiter carries the same value. It used to read as the text `""`,
    /// because the node re-counted the quotes from the outside in and stopped early on a
    /// short body; the grammar now tells it which delimiter it matched.
    /// </remarks>
    [TestMethod]
    public void FourQuotesAreTheEmptyStringDoubled()
    {
        var text = (StringConstant)ValueOfFirstArgument("echo \"\"\"\"");

        Assert.AreEqual(string.Empty, text.Value);
        Assert.AreEqual(2, text.QuoteCount);
    }

    [DataTestMethod]
    [DataRow("echo \"a\"", "a", 1)]
    [DataRow("echo \"\"a\"\"", "a", 2)]
    [DataRow("echo \"\"\"a\"\"\"", "a", 3)]
    [DataRow("echo \"\"\"\"\"\"", "", 3)]
    public void EveryDelimiterCarriesTheSameValue(string source, string value, int quotes)
    {
        var text = (StringConstant)ValueOfFirstArgument(source);

        Assert.AreEqual(value, text.Value);
        Assert.AreEqual(quotes, text.QuoteCount);
    }

    [TestMethod]
    public void DoubledQuotesAreTrimmedAndCounted()
    {
        // StringLiteral3 = '""' ... '""'. StringConstant records how many quotes wrapped
        // the value so the serialiser can put the same ones back.
        var text = (StringConstant)ValueOfFirstArgument("echo \"\"doubled\"\"");

        Assert.AreEqual("doubled", text.Value);
        Assert.AreEqual(2, text.QuoteCount);
    }

    [TestMethod]
    public void StringMayContainSpacesAndPunctuation() =>
        Assert.AreEqual("a, b; c: d!", ((StringConstant)ValueOfFirstArgument("echo \"a, b; c: d!\"")).Value);

    [TestMethod]
    public void UnterminatedStringIsRejected() => ParserHarness.ParseError("echo \"unterminated");

    // VariableReference ::= '$' VariableName

    [TestMethod]
    public void VariableReferenceDropsTheSigil()
    {
        var reference = (VariableReference)ValueOfFirstArgument("command $target");

        Assert.AreEqual("target", reference.Name.Name);
    }

    [TestMethod]
    public void VariableReferenceNeedsAName() => ParserHarness.ParseError("command $");

    private static Value ValueOfFirstArgument(string source)
    {
        var argument = ParserHarness.Cli(source).Arguments.Arguments[0];

        return argument switch
        {
            RequiredCommandArgument required => required.Value,
            CommandArgumentValue value => value.Value,
            _ => throw new AssertFailedException($"Unexpected argument {argument.GetType().Name}."),
        };
    }
}
