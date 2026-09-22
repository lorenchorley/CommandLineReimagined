using Commands.Parser.SemanticTree;

namespace Parser.Tests;

/// <summary>
/// The three shapes a command can take, and how they compose through pipes.
/// </summary>
[TestClass]
public class CommandFormTests
{
    // <Program> ::= <PipedCommandList> | ! Empty

    [TestMethod]
    public void EmptyInputIsAnEmptyCommand() =>
        Assert.IsInstanceOfType(ParserHarness.Parse(string.Empty), typeof(EmptyCommand));

    [TestMethod]
    public void WhitespaceOnlyInputIsAnEmptyCommand() =>
        Assert.IsInstanceOfType(ParserHarness.Parse("   "), typeof(EmptyCommand));

    // <CommandExpression_CLINotation> ::= <ID> <CommandArgumentList>

    [TestMethod]
    public void BareCommandHasNoArguments() =>
        Assert.AreEqual(0, ParserHarness.Cli("command").Arguments.Arguments.Count);

    [TestMethod]
    public void CliArgumentsAreOrdered()
    {
        var arguments = ParserHarness.Cli("command first second third").Arguments.Arguments;

        Assert.AreEqual(3, arguments.Count);
        CollectionAssert.AreEqual(
            new[] { "first", "second", "third" },
            arguments.Select(NameOf).ToArray());
    }

    [TestMethod]
    public void FlagsAndValuesMayBeInterleaved()
    {
        // <CommandArgument> ::= <Flag> | <Value>, so order is whatever was written.
        var arguments = ParserHarness.Cli("command -a one -b two").Arguments.Arguments;

        Assert.AreEqual(4, arguments.Count);
        Assert.IsInstanceOfType(arguments[0], typeof(CommandArgumentFlag));
        Assert.IsInstanceOfType(arguments[1], typeof(CommandArgument));
        Assert.IsInstanceOfType(arguments[2], typeof(CommandArgumentFlag));
    }

    [TestMethod]
    public void TrailingFlagIsAllowed() =>
        Assert.AreEqual(2, ParserHarness.Cli("command value -flag").Arguments.Arguments.Count);

    // <FunctionExpression> ::= <ID> '(' <FunctionArgumentList> ')' | <ID> '(' ')'

    [TestMethod]
    public void EmptyArgumentListIsAFunctionCall()
    {
        var function = ParserHarness.Function("command()");

        Assert.AreEqual("command", function.Id.Name);
        Assert.AreEqual(0, function.Arguments.Arguments.Count);
    }

    [TestMethod]
    public void SingleRequiredArgumentIsAFunctionCall()
    {
        var function = ParserHarness.Function("command(value)");

        Assert.AreEqual(1, function.Arguments.Arguments.Count);
        Assert.IsInstanceOfType(function.Arguments.Arguments[0], typeof(RequiredCommandArgument));
    }

    // <OptionalArgument> ::= <ID> ':' <Value>

    [TestMethod]
    public void NamedArgumentIsAnOptionalArgument()
    {
        var argument = (OptionalCommandArgument)ParserHarness.Function("command(name: value)").Arguments.Arguments[0];

        Assert.AreEqual("name", argument.Name.Match(flag => flag.Name, identifier => identifier.Name));
        Assert.AreEqual("value", ((Identifier)argument.Value).Name);
    }

    [TestMethod]
    public void FunctionWithoutParenthesesIsCliNotation()
    {
        // `command value` and `command(value)` are different productions, not sugar.
        Assert.IsTrue(ParserHarness.SingleCommand("command value").Expression.IsT1);
        Assert.IsTrue(ParserHarness.SingleCommand("command(value)").Expression.IsT0);
    }

    // <IndividualCLIValue> ::= <InstanceTag>

    [TestMethod]
    public void BareTagIsACommandOnItsOwn() =>
        Assert.AreEqual("thing", ParserHarness.Instance("<thing/>").ObjectType.Value);

    [TestMethod]
    public void BareConstantIsNotACommand()
    {
        // A command expression must start with an identifier or a tag, so a lone string
        // is not a program even though it is a valid Value elsewhere.
        ParserHarness.ParseError("\"just a string\"");
    }

    // <PipedCommandList> ::= <PipedCommandList> '|' <CommandExpression> | <CommandExpression>

    [TestMethod]
    public void PipeJoinsTwoCommands() =>
        Assert.AreEqual(2, Pipeline("first | second").OrderedCommands.Count);

    [TestMethod]
    public void PipesChainLeftToRight()
    {
        var pipeline = Pipeline("a | b | c");

        Assert.AreEqual(3, pipeline.OrderedCommands.Count);
        CollectionAssert.AreEqual(
            new[] { "a", "b", "c" },
            pipeline.OrderedCommands.Select(c => c.Expression.AsT1.Name.Name).ToArray());
    }

    [TestMethod]
    public void PipedCommandsKeepTheirOwnArguments()
    {
        var pipeline = Pipeline("first one | second two");

        Assert.AreEqual("one", NameOf(pipeline.OrderedCommands[0].Expression.AsT1.Arguments.Arguments[0]));
        Assert.AreEqual("two", NameOf(pipeline.OrderedCommands[1].Expression.AsT1.Arguments.Arguments[0]));
    }

    [TestMethod]
    public void FormsMayBeMixedAcrossAPipe()
    {
        var pipeline = Pipeline("command() | other value | <thing/>");

        Assert.IsTrue(pipeline.OrderedCommands[0].Expression.IsT0);
        Assert.IsTrue(pipeline.OrderedCommands[1].Expression.IsT1);
        Assert.IsTrue(pipeline.OrderedCommands[2].Expression.IsT2);
    }

    // Decision 0022: a command's name may be several identifiers joined by hyphens.

    [TestMethod]
    public void AHyphenJoinsTwoWordsIntoOneCommandName() =>
        Assert.AreEqual("save-view", ParserHarness.Cli("save-view weekend").Name.Name);

    [TestMethod]
    public void AHyphenatedNameKeepsItsArguments() =>
        Assert.AreEqual(1, ParserHarness.Cli("save-view weekend").Arguments.Arguments.Count);

    [TestMethod]
    public void AHyphenatedNameWorksInTheFunctionForm() =>
        Assert.AreEqual("from-xml", ParserHarness.Function("from-xml(items.xml)").Id.Name);

    /// A space before the hyphen ends the name, which is what keeps flags flags.
    [TestMethod]
    public void ASpacedHyphenIsStillAFlag()
    {
        var command = ParserHarness.Cli("ls -l");

        Assert.AreEqual("ls", command.Name.Name);
        Assert.IsInstanceOfType(command.Arguments.Arguments[0], typeof(CommandArgumentFlag));
    }

    /// And a negative number after one is still a number (decision 0007).
    [TestMethod]
    public void ASpacedHyphenBeforeADigitIsStillANumber() =>
        Assert.AreEqual("-5", NameOf(ParserHarness.Cli("echo -5").Arguments.Arguments[0]));

    [TestMethod]
    public void AHyphenatedNameRoundTrips() => ParserHarness.AssertRoundTrips("save-view weekend");

    [TestMethod]
    public void ATrailingHyphenIsNotPartOfTheName() =>
        Assert.AreEqual("echo", ParserHarness.Cli("echo a-").Name.Name);

    [TestMethod]
    public void TrailingPipeIsRejected() => ParserHarness.ParseError("command |");

    [TestMethod]
    public void LeadingPipeIsRejected() => ParserHarness.ParseError("| command");

    [TestMethod]
    public void EmptyPipeStageIsRejected() => ParserHarness.ParseError("a | | b");

    private static PipedCommandList Pipeline(string source) =>
        (PipedCommandList)ParserHarness.Parse(source);

    private static string NameOf(CommandArgument argument)
    {
        Value value = argument switch
        {
            RequiredCommandArgument required => required.Value,
            CommandArgumentValue wrapped => wrapped.Value,
            _ => throw new AssertFailedException($"Unexpected argument {argument.GetType().Name}."),
        };

        return ((Identifier)value).Name;
    }
}
