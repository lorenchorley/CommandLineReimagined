using Commands.Parser.SemanticTree;

namespace Parser.Tests;

/// <summary>
/// Phase 5's grammar: <c>else</c>, <c>try</c>, <c>??</c>, and a pipeline in
/// parentheses as a stage or an operand.
/// </summary>
/// <remarks>
/// FParsec only. None of this exists in the GOLD grammar, so none of it belongs in the
/// equivalence corpus (decision 0014, decision 0023).
/// </remarks>
[TestClass]
public class RecoveryTests
{
    private static RecoveryLine Recovery(string source) =>
        ParserHarness.Parse(source) as RecoveryLine
            ?? throw new AssertFailedException($"'{source}' did not parse as a line with else in it.");

    private static string[] Names(PipedCommandList pipeline) =>
        pipeline.OrderedCommands.Select(c => c.Expression.AsT1.Name.Name).ToArray();

    // ------------------------------------------------------------------- else

    [TestMethod]
    public void ElseJoinsTwoPipelines()
    {
        var line = Recovery("cat notes.txt else echo none");

        Assert.AreEqual(2, line.Pipelines.Count);
        CollectionAssert.AreEqual(new[] { "cat" }, Names(line.Pipelines[0]));
        CollectionAssert.AreEqual(new[] { "echo" }, Names(line.Pipelines[1]));
    }

    /// <summary><c>else</c> binds looser than <c>|</c>: <c>a | b else c | d</c> is two pipelines of two.</summary>
    [TestMethod]
    public void ElseBindsLooserThanThePipe()
    {
        var line = Recovery("a | b else c | d");

        CollectionAssert.AreEqual(new[] { "a", "b" }, Names(line.Pipelines[0]));
        CollectionAssert.AreEqual(new[] { "c", "d" }, Names(line.Pipelines[1]));
    }

    [TestMethod]
    public void ElseCanBeChained()
    {
        Assert.AreEqual(3, Recovery("a else b else c").Pipelines.Count);
    }

    /// <summary>A line without <c>else</c> is the pipeline itself, as it was before Phase 5.</summary>
    [TestMethod]
    public void ALineWithoutElseIsStillAPipeline()
    {
        Assert.IsInstanceOfType<PipedCommandList>(ParserHarness.Parse("a | b"));
    }

    [TestMethod]
    public void ElseEndsAnArgumentList()
    {
        var line = Recovery("echo one two else echo three");

        Assert.AreEqual(2, line.Pipelines[0].OrderedCommands[0].Expression.AsT1.Arguments.Arguments.Count);
    }

    /// <summary>A word that merely starts with <c>else</c> is an ordinary word.</summary>
    [TestMethod]
    public void ElsewhereIsAWord()
    {
        Assert.IsInstanceOfType<PipedCommandList>(ParserHarness.Parse("echo elsewhere"));
    }

    /// <summary>Decision 0019: a reserved word cannot name a command, and the error says so.</summary>
    [DataTestMethod]
    [DataRow("else echo x", "else")]
    [DataRow("eq x", "eq")]
    [DataRow("and(x)", "and")]
    public void AReservedWordCannotNameACommand(string source, string word)
    {
        var error = ParserHarness.ParseError(source);

        Assert.AreEqual(0, error.Column);
        Assert.AreEqual($"'{word}' is a reserved word and cannot name a command", error.Explanation);
    }

    [DataTestMethod]
    [DataRow("else echo x")]
    [DataRow("cat x else")]
    [DataRow("cat x else else echo y")]
    [DataRow("cat x | else echo y")]
    [DataRow("echo (a else b)")]
    public void ElseNeedsAPipelineOnBothSides(string source)
    {
        ParserHarness.ParseError(source);
    }

    // -------------------------------------------------------------------- try

    [TestMethod]
    public void TryMarksOneStage()
    {
        var pipeline = (PipedCommandList)ParserHarness.Parse("try cat x | set problem");

        Assert.IsTrue(pipeline.OrderedCommands[0].Try);
        Assert.IsFalse(pipeline.OrderedCommands[1].Try);
        Assert.AreEqual("cat", pipeline.OrderedCommands[0].Expression.AsT1.Name.Name);
    }

    [TestMethod]
    public void TryCanMarkALaterStage()
    {
        var pipeline = (PipedCommandList)ParserHarness.Parse("ls | try first | set f");

        Assert.IsTrue(pipeline.OrderedCommands[1].Try);
    }

    /// <summary>Decision 0014's own example: <c>try</c> in front of a pipeline in parentheses.</summary>
    [TestMethod]
    public void TryCanMarkAParenthesisedPipeline()
    {
        var stage = ((PipedCommandList)ParserHarness.Parse("try (cat notes.txt) | set r")).OrderedCommands[0];

        Assert.IsTrue(stage.Try);
        Assert.IsTrue(stage.Expression.IsT3);
    }

    [TestMethod]
    public void TryingIsACommandNameNotTryAndIng()
    {
        var stage = ParserHarness.SingleCommand("trying");

        Assert.IsFalse(stage.Try);
        Assert.AreEqual("trying", stage.Expression.AsT1.Name.Name);
    }

    [DataTestMethod]
    [DataRow("try")]
    [DataRow("try | echo")]
    [DataRow("cat try")]
    public void TryNeedsAStageAfterIt(string source)
    {
        ParserHarness.ParseError(source);
    }

    // --------------------------------------------------------------------- ??

    [TestMethod]
    public void TheDefaultBelongsToTheStageBeforeIt()
    {
        var pipeline = (PipedCommandList)ParserHarness.Parse("first (ls) ?? \"none\" | set latest");

        var stage = pipeline.OrderedCommands[0];
        Assert.AreEqual("first", stage.Expression.AsT1.Name.Name);
        Assert.AreEqual("none", ((StringConstant)stage.Default!).Value);
        Assert.IsNull(pipeline.OrderedCommands[1].Default);
    }

    [TestMethod]
    public void TheDefaultCanBeAParenthesisedPipeline()
    {
        var stage = ParserHarness.SingleCommand("first (ls) ?? (echo none)");

        Assert.IsInstanceOfType<NestedPipeline>(stage.Default);
    }

    /// <summary>One <c>??</c> per stage; a second is a syntax error rather than a chain.</summary>
    [DataTestMethod]
    [DataRow("first ?? a ?? b")]
    [DataRow("first ??")]
    [DataRow("?? a")]
    [DataRow("first ? a")]
    public void ADefaultIsOneOperand(string source)
    {
        ParserHarness.ParseError(source);
    }

    [TestMethod]
    public void TryAndADefaultCanShareAStage()
    {
        var stage = ParserHarness.SingleCommand("try first ?? none");

        Assert.IsTrue(stage.Try);
        Assert.IsNotNull(stage.Default);
    }

    // ------------------------------------------------- nested pipelines (0023)

    /// <summary><c>first (ls)</c> is <c>first</c> given a pipeline's result, not a function call.</summary>
    [TestMethod]
    public void ASpacedParenthesisIsANestedPipeline()
    {
        var cli = ParserHarness.Cli("first (ls | where $row.kind eq view)");

        var nested = ((CommandArgumentValue)cli.Arguments.Arguments[0]).Value as NestedPipeline;
        Assert.IsNotNull(nested);
        Assert.AreEqual(2, nested.Pipeline.OrderedCommands.Count);
    }

    [TestMethod]
    public void AnAdjacentParenthesisIsStillAFunctionCall()
    {
        Assert.AreEqual(1, ParserHarness.Function("first(ls)").Arguments.Arguments.Count);
    }

    [TestMethod]
    public void AParenthesisedPipelineCanStandAsAStage()
    {
        var pipeline = (PipedCommandList)ParserHarness.Parse("ls | (where $row.kind eq folder | count)");

        Assert.IsTrue(pipeline.OrderedCommands[1].Expression.IsT3);
    }

    [TestMethod]
    public void NestedPipelinesNest()
    {
        var outer = ParserHarness.Cli("echo (echo (ls | count))");
        var middle = (NestedPipeline)((CommandArgumentValue)outer.Arguments.Arguments[0]).Value;
        var inner = middle.Pipeline.OrderedCommands[0].Expression.AsT1;

        Assert.IsInstanceOfType<NestedPipeline>(((CommandArgumentValue)inner.Arguments.Arguments[0]).Value);
    }

    // ----------------------------------------------------------- round trips

    [DataTestMethod]
    [DataRow("cat notes-from-yesterday.txt else echo \"starting fresh\"")]
    [DataRow("cat notes-from-yesterday.txt else echo \"starting fresh\" | write today.txt")]
    [DataRow("try cat nowhere.txt | set problem")]
    [DataRow("first (ls | where $row.kind eq view) ?? \"no views yet\"")]
    [DataRow("first (ls | where $row.kind eq view) ?? \"no views yet\" | set latest")]
    [DataRow("mkdir today | cd nowhere else echo \"the whole line was rolled back\"")]
    [DataRow("try (cat notes.txt) | set r")]
    [DataRow("a else b else c")]
    [DataRow("ls | (where $row.kind eq folder | count)")]
    [DataRow("try first ?? (echo none)")]
    public void Phase5LinesSurviveARoundTrip(string source)
    {
        ParserHarness.AssertRoundTrips(source);
    }

    /// <summary>Every line of the example program parses (examples.md, resilient.clr).</summary>
    [TestMethod]
    public void TheResilientProgramParses()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "examples", "resilient.clr");

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            ParserHarness.AssertRoundTrips(line);
        }
    }
}
