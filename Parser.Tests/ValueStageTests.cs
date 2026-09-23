using Commands.Parser.SemanticTree;

namespace Parser.Tests;

/// <summary>
/// Phase 8's grammar: a variable may stand as a stage.
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
}
