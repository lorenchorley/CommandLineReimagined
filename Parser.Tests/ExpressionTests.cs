using Commands.Parser.SemanticTree;

namespace Parser.Tests;

/// <summary>
/// The Phase 3 grammar: word operators, member access, and the reserved words that make
/// both unambiguous.
/// </summary>
/// <remarks>
/// Every case here is one both parsers cannot agree on, because the GOLD grammar has no
/// expressions at all. They belong in the FParsec-only classes, which is where the
/// equivalence suite's corpus stops.
/// </remarks>
[TestClass]
public class ExpressionTests
{
    /// <summary>The value written for the single argument of a one-command line.</summary>
    private static Value Argument(string source, int index = 0)
    {
        var arguments = ParserHarness.Cli(source).Arguments.Arguments;
        Assert.IsTrue(arguments.Count > index, $"'{source}' has no argument {index}.");

        return arguments[index] is CommandArgumentValue value
            ? value.Value
            : throw new AssertFailedException($"argument {index} of '{source}' is not a plain value.");
    }

    // ----------------------------------------------------------- Comparisons

    [DataTestMethod]
    [DataRow("where $row.size eq 1", "eq")]
    [DataRow("where $row.size ne 1", "ne")]
    [DataRow("where $row.size gt 1", "gt")]
    [DataRow("where $row.size ge 1", "ge")]
    [DataRow("where $row.size lt 1", "lt")]
    [DataRow("where $row.size le 1", "le")]
    [DataRow("where $row.name like note", "like")]
    [DataRow("where $row.tags has work", "has")]
    public void EveryComparisonWordParses(string source, string expected)
    {
        var comparison = Argument(source) as ComparisonExpression;

        Assert.IsNotNull(comparison, $"'{source}' did not produce a comparison.");
        Assert.AreEqual(expected, comparison.Operator.Name);
    }

    /// <summary>
    /// A word that merely begins with an operator is an argument, not an operator.
    /// </summary>
    /// <remarks>
    /// Decision 0019 reserves the exact word. Without the boundary check `equals` would
    /// parse as `eq` followed by `uals`, which is the classic way a keyword list eats
    /// the language around it.
    /// </remarks>
    [DataTestMethod]
    [DataRow("echo equals")]
    [DataRow("echo notes.txt")]
    [DataRow("echo orders")]
    [DataRow("echo lease")]
    [DataRow("echo Eq")]
    public void AWordThatOnlyStartsWithAnOperatorIsStillAWord(string source)
    {
        Assert.IsInstanceOfType<Identifier>(Argument(source));
    }

    // ------------------------------------------------------------ Precedence

    /// <summary>`and` binds tighter than `or`, so the tree is or(a, and(b, c)).</summary>
    [TestMethod]
    public void AndBindsTighterThanOr()
    {
        var top = Argument("where $a or $b and $c") as BooleanExpression;

        Assert.IsNotNull(top);
        Assert.AreEqual("or", top.Operator.Name);
        Assert.IsInstanceOfType<VariableReference>(top.Left);

        var right = top.Right as BooleanExpression;
        Assert.IsNotNull(right);
        Assert.AreEqual("and", right.Operator.Name);
    }

    [TestMethod]
    public void BooleanOperatorsAreLeftAssociative()
    {
        var top = Argument("where $a and $b and $c") as BooleanExpression;

        Assert.IsNotNull(top);
        Assert.IsInstanceOfType<BooleanExpression>(top.Left);
        Assert.IsInstanceOfType<VariableReference>(top.Right);
    }

    /// <summary>`not` takes the whole comparison after it, not just its first operand.</summary>
    [TestMethod]
    public void NotAppliesToTheComparisonAfterIt()
    {
        var negation = Argument("where not $row.kind eq folder") as NotExpression;

        Assert.IsNotNull(negation);
        Assert.IsInstanceOfType<ComparisonExpression>(negation.Operand);
    }

    [TestMethod]
    public void ComparisonsCombineWithAnd()
    {
        var top = Argument("where $row.kind eq note and $row.tag eq work") as BooleanExpression;

        Assert.IsNotNull(top);
        Assert.IsInstanceOfType<ComparisonExpression>(top.Left);
        Assert.IsInstanceOfType<ComparisonExpression>(top.Right);
    }

    /// <summary>
    /// An operand on its own is the operand, not an expression wrapped around one.
    /// </summary>
    /// <remarks>
    /// This is what keeps every line written before Phase 3 parsing to the tree it
    /// always did: `cd documents` has not become an expression because expressions
    /// exist.
    /// </remarks>
    [DataTestMethod]
    [DataRow("cd documents")]
    [DataRow("echo 5")]
    [DataRow("echo $v")]
    public void AnOperandWithNoOperatorIsNotAnExpression(string source)
    {
        Assert.IsNotInstanceOfType<ExpressionNode>(Argument(source));
    }

    // -------------------------------------------------------- Member access

    [TestMethod]
    public void AVariableCanReadAMember()
    {
        var reference = Argument("echo $row.size") as VariableReference;

        Assert.IsNotNull(reference);
        Assert.AreEqual("row", reference.Name.Name);
        Assert.AreEqual(1, reference.Members.Count);
        Assert.AreEqual("size", reference.Members[0].Name);
    }

    [TestMethod]
    public void MembersNest()
    {
        var reference = Argument("echo $a.b.c") as VariableReference;

        Assert.IsNotNull(reference);
        CollectionAssert.AreEqual(new[] { "b", "c" }, reference.Members.Select(m => m.Name).ToArray());
    }

    [TestMethod]
    public void AVariableWithNoMembersHasNone()
    {
        var reference = Argument("echo $row") as VariableReference;

        Assert.IsNotNull(reference);
        Assert.AreEqual(0, reference.Members.Count);
    }

    // ------------------------------------------------------- Reserved words

    /// <summary>
    /// Decision 0019: a reserved word is never an argument, in any position.
    /// </summary>
    [DataTestMethod]
    [DataRow("echo eq")]
    [DataRow("echo and")]
    [DataRow("echo not")]
    [DataRow("echo has")]
    [DataRow("echo else")]
    [DataRow("echo try")]
    [DataRow("<thing a=eq/>")]
    public void AReservedWordIsNotAnArgument(string source)
    {
        ParserHarness.ParseError(source);
    }

    /// <summary>Quoting is how a reserved word is passed as data.</summary>
    [TestMethod]
    public void AQuotedReservedWordIsText()
    {
        var text = Argument("echo \"eq\"") as StringConstant;

        Assert.IsNotNull(text);
        Assert.AreEqual("eq", text.Value);
    }

    // ------------------------------------------------------ Nested pipelines

    /// <remarks>
    /// The parenthesis is written after a first argument, because a name with a space
    /// and a parenthesis after it is still the function form until decision 0022
    /// separates the two in Phase 5. Where no name precedes it, the parenthesis can
    /// only be an operand, and that is the case this pins.
    /// </remarks>
    [TestMethod]
    public void AParenthesisedPipelineIsAnOperand()
    {
        var nested = Argument("echo x (ls | count)", 1) as NestedPipeline;

        Assert.IsNotNull(nested);
        Assert.AreEqual(2, nested.Pipeline.OrderedCommands.Count);
    }

    [TestMethod]
    public void AParenthesisedPipelineCanBeComparedAgainst()
    {
        var comparison = Argument("where $row.size gt (ls | count)") as ComparisonExpression;

        Assert.IsNotNull(comparison);
        Assert.IsInstanceOfType<NestedPipeline>(comparison.Right);
    }

    /// <summary>A pipe inside parentheses belongs to the nested pipeline.</summary>
    [TestMethod]
    public void ANestedPipelineDoesNotSplitTheOuterOne()
    {
        var tree = (PipedCommandList)ParserHarness.Parse("echo x (ls | count) | set n");

        Assert.AreEqual(2, tree.OrderedCommands.Count);
    }

    /// <summary>
    /// The function form needs its parenthesis on the name: `first(ls)` calls `first`.
    /// </summary>
    /// <remarks>
    /// Telling the two apart by the space is decision 0022's business, in Phase 5. Until
    /// then the function form wins wherever a name precedes a parenthesis, which is what
    /// this pins so that the change is visible when it is made.
    /// </remarks>
    [TestMethod]
    public void ANameBeforeAParenthesisIsStillAFunctionCall()
    {
        Assert.AreEqual("first", ParserHarness.Function("first(ls)").Id.Name);
    }

    // ---------------------------------------------------------- Round trips

    [DataTestMethod]
    [DataRow("where $row.size gt 100")]
    [DataRow("where $row.kind eq folder")]
    [DataRow("where $row.name like note")]
    [DataRow("where $a and $b")]
    [DataRow("where $a or $b")]
    [DataRow("where $a or $b and $c")]
    [DataRow("where not $row.kind eq folder")]
    [DataRow("where $row.kind eq note and $row.tag eq work")]
    [DataRow("echo $row.size")]
    [DataRow("echo $a.b.c")]
    [DataRow("echo x (ls | count)")]
    [DataRow("where $row.size gt (ls | count)")]
    [DataRow("ls | where $row.kind eq folder | count")]
    public void ExpressionsSurviveARoundTrip(string source)
    {
        ParserHarness.AssertRoundTrips(source);
    }
}
