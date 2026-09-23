using Commands.Parser.SemanticTree;

namespace Parser.Tests;

/// <summary>
/// Phase 8's grammar: a variable may stand as a stage, a stop after a variable must be
/// followed by a name, and a comparison operator with nothing after it says what is
/// missing.
/// </summary>
/// <remarks>
/// FParsec only (decision 0032). The GOLD grammar has no value stage and is frozen, so
/// none of this belongs in the equivalence corpus.
/// </remarks>
[TestClass]
public class ValueStageTests
{
    private static VariableReference ValueStage(CommandExpression stage)
    {
        Assert.IsTrue(stage.Expression.IsT4, "The stage is not a value stage.");
        return stage.Expression.AsT4;
    }

    private static PipedCommandList Pipeline(string source) =>
        ParserHarness.Parse(source) as PipedCommandList
            ?? throw new AssertFailedException($"'{source}' did not parse as a pipeline.");

    // ------------------------------------------------------------ value stages

    [TestMethod]
    public void AVariableCanStandAsAStage()
    {
        var reference = ValueStage(ParserHarness.SingleCommand("$files"));

        Assert.AreEqual("files", reference.Name.Name);
        Assert.AreEqual(0, reference.Members.Count);
    }

    [TestMethod]
    public void AValueStageCanReadMembers()
    {
        var reference = ValueStage(ParserHarness.SingleCommand("$problem.kind"));

        Assert.AreEqual("problem", reference.Name.Name);
        CollectionAssert.AreEqual(new[] { "kind" }, reference.Members.Select(m => m.Name).ToArray());
    }

    /// <summary>A value stage heads a pipeline, and the stages after it are commands.</summary>
    [TestMethod]
    public void AValueStageFeedsAPipeline()
    {
        var pipeline = Pipeline("$files | where $row.size gt 10");

        Assert.AreEqual(2, pipeline.OrderedCommands.Count);
        Assert.AreEqual("files", ValueStage(pipeline.OrderedCommands[0]).Name.Name);
        Assert.AreEqual("where", pipeline.OrderedCommands[1].Expression.AsT1.Name.Name);
    }

    /// <summary>Decision 0014's example: <c>??</c> belongs to a value stage as to any.</summary>
    [TestMethod]
    public void AValueStageTakesADefault()
    {
        var stage = ParserHarness.SingleCommand("$maybe ?? \"x\"");

        Assert.AreEqual("maybe", ValueStage(stage).Name.Name);
        Assert.AreEqual("x", (stage.Default as StringConstant)?.Value);
    }

    [TestMethod]
    public void AValueStageCanBeTried()
    {
        var stage = ParserHarness.SingleCommand("try $v");

        Assert.IsTrue(stage.Try);
        Assert.AreEqual("v", ValueStage(stage).Name.Name);
    }

    [TestMethod]
    public void AValueStageCanBeRecoveredFrom()
    {
        var line = ParserHarness.Parse("$v else echo none") as RecoveryLine;

        Assert.IsNotNull(line);
        Assert.AreEqual("v", ValueStage(line.Pipelines[0].OrderedCommands[0]).Name.Name);
    }

    /// <summary>A value stage may stand after a pipe too; it is a stage like any other.</summary>
    [TestMethod]
    public void AValueStageCanFollowAPipe()
    {
        var pipeline = Pipeline("ls | $v");

        Assert.AreEqual("v", ValueStage(pipeline.OrderedCommands[1]).Name.Name);
    }

    [TestMethod]
    [DataRow("$v")]
    [DataRow("$files | count")]
    [DataRow("$problem.kind")]
    [DataRow("$maybe ?? \"x\"")]
    [DataRow("try $v | set p")]
    [DataRow("$files | where $row.size gt 10")]
    [DataRow("$v else echo none")]
    public void AValueStageRoundTrips(string source) => ParserHarness.AssertRoundTrips(source);

    /// <summary>A value is not a command: it takes no arguments.</summary>
    [TestMethod]
    public void AValueStageTakesNoArguments()
    {
        ParserHarness.ParseError("$v 5");
    }

    /// <summary>A sigil with no name after it is still not a variable.</summary>
    [TestMethod]
    [DataRow("$")]
    [DataRow("command $")]
    [DataRow("ls | $")]
    public void ADollarWithNoNameIsRejected(string source) => ParserHarness.ParseError(source);

    // ------------------------------------------------------------ the stop

    /// <summary>
    /// A stop after a variable belongs to a member, so a stop with no name after it is
    /// an error rather than a path called <c>.</c>.
    /// </summary>
    [TestMethod]
    [DataRow("ls | where $row.", 16)]
    [DataRow("echo $v.", 8)]
    [DataRow("$problem.", 9)]
    [DataRow("ls | where $row. eq folder", 16)]
    public void AStopMustBeFollowedByAName(string source, int column)
    {
        var error = ParserHarness.ParseError(source);

        Assert.AreEqual(column, error.Column);
        Assert.AreEqual("a column name belongs after the stop, as in $row.kind", error.Explanation);
    }

    /// <summary>A name after the stop is a member, as it always was, digits included.</summary>
    [TestMethod]
    public void AStopFollowedByANameIsAMember()
    {
        var pipeline = Pipeline("echo $v.1");
        var argument = (CommandArgumentValue)pipeline.OrderedCommands[0].Expression.AsT1.Arguments.Arguments[0];
        var reference = (VariableReference)argument.Value;

        CollectionAssert.AreEqual(new[] { "1" }, reference.Members.Select(m => m.Name).ToArray());
    }

    /// <summary>A stop that is not after a variable is still an ordinary word.</summary>
    [TestMethod]
    [DataRow("cd ..")]
    [DataRow("cat notes.txt")]
    [DataRow("ls .")]
    public void AStopInAWordIsStillAWord(string source) => ParserHarness.AssertRoundTrips(source);

    // ------------------------------------------------------------ the operator

    /// <summary>An operator with nothing after it says what it needs, naming itself.</summary>
    [TestMethod]
    [DataRow("ls | where $row.kind eq", "eq needs a value to compare with, such as folder", 23)]
    [DataRow("ls | where $row.kind ne", "ne needs a value to compare with, such as folder", 23)]
    [DataRow("ls | where $row.size gt", "gt needs a value to compare with, such as 10", 23)]
    [DataRow("ls | where $row.name like", "like needs a value to compare with, such as \"*.txt\"", 25)]
    [DataRow("ls | where $row.kind eq | count", "eq needs a value to compare with, such as folder", 24)]
    [DataRow("ls | where not $row.kind eq", "eq needs a value to compare with, such as folder", 27)]
    [DataRow("ls | where $row.kind eq folder or $row.size lt", "lt needs a value to compare with, such as 10", 46)]
    public void AnOperatorWithNothingAfterItExplainsItself(string source, string explanation, int column)
    {
        var error = ParserHarness.ParseError(source);

        Assert.AreEqual(explanation, error.Explanation);
        Assert.AreEqual(column, error.Column);
    }

    /// <summary>
    /// An operand that was started and went wrong keeps its own error: a reserved word
    /// says how to write it as text, and an unclosed quote is not a missing value.
    /// </summary>
    [TestMethod]
    public void AStartedOperandKeepsItsOwnError()
    {
        Assert.AreEqual(
            "'eq' is an operator; write \"eq\" to pass it as text",
            ParserHarness.ParseError("ls | where $row.kind eq eq").Explanation);

        Assert.IsNull(ParserHarness.ParseError("ls | where $row.kind eq \"folder").Explanation);
    }
}
