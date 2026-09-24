using System.Linq;
using Commands.Parser.SemanticTree;

namespace Parser.Tests;

/// <summary>
/// Decision 0048: a member may be written with <c>@</c> before its name, for a tag's own
/// parts, <c>$v.@tag</c> and <c>$v.@children</c>.
/// </summary>
/// <remarks>
/// The original GOLD grammar had no member access at all, so none of this is in the core
/// corpus of CoreGrammarTests.
/// </remarks>
[TestClass]
public class OwnMemberTests
{
    private static Value Argument(string source, int index = 0)
    {
        var arguments = ParserHarness.Cli(source).Arguments.Arguments;
        Assert.IsTrue(arguments.Count > index, $"'{source}' has no argument {index}.");

        return arguments[index] is CommandArgumentValue value
            ? value.Value
            : throw new AssertFailedException($"argument {index} of '{source}' is not a plain value.");
    }

    [TestMethod]
    [DataRow("echo $v.@tag", "@tag")]
    [DataRow("echo $v.@children", "@children")]
    [DataRow("echo $v.@other", "@other")]
    public void AnAtMemberIsNamedWithItsAt(string source, string expected)
    {
        var reference = Argument(source) as VariableReference;

        Assert.IsNotNull(reference, $"'{source}' did not produce a variable reference.");
        Assert.AreEqual("v", reference.Name.Name);
        Assert.AreEqual(1, reference.Members.Count);
        Assert.AreEqual(expected, reference.Members[0].Name);
    }

    [TestMethod]
    public void AtMembersNestWithPlainOnes()
    {
        var reference = Argument("echo $d.@children.@tag.x") as VariableReference;

        Assert.IsNotNull(reference);
        CollectionAssert.AreEqual(new[] { "@children", "@tag", "x" }, reference.Members.Select(m => m.Name).ToArray());
    }

    [TestMethod]
    public void AnAtMemberIsAnOperandOfAComparison()
    {
        var comparison = Argument("where $row.@tag eq book") as ComparisonExpression;

        Assert.IsNotNull(comparison);
        Assert.AreEqual("eq", comparison.Operator.Name);

        var left = comparison.Left as VariableReference;
        Assert.IsNotNull(left);
        Assert.AreEqual("row", left.Name.Name);
        Assert.AreEqual("@tag", left.Members.Single().Name);
        Assert.AreEqual("book", ((Identifier)comparison.Right).Name);
    }

    [TestMethod]
    public void AnAtMemberCanStartAValueStage()
    {
        var reference = ParserHarness.Parse("$v.@children | count");

        StringAssert.Contains(ParserHarness.Serialise(reference), "$v.@children");
    }

    [TestMethod]
    [DataRow("echo $v.@tag")]
    [DataRow("echo $v.@children")]
    [DataRow("where $row.@tag eq book")]
    [DataRow("echo $d.@children.@tag")]
    public void AnAtMemberRoundTrips(string source) => ParserHarness.AssertRoundTrips(source);

    /// <summary>An <c>@</c> inside a word is still part of the word.</summary>
    [TestMethod]
    [DataRow("echo user@host", "user@host")]
    [DataRow("echo a.@b", "a.@b")]
    // Decision 0050: a word may start with @, so a column pick answers is written as shown.
    [DataRow("select @tag", "@tag")]
    [DataRow("sort @children", "@children")]
    [DataRow("echo @", "@")]
    public void AnAtOutsideAMemberIsPartOfAWord(string source, string expected)
    {
        Assert.AreEqual(expected, ((Identifier)Argument(source)).Name);
    }

    /// <summary>An <c>@</c> word is an argument like any other, in any position (decision 0050).</summary>
    [TestMethod]
    [DataRow("ls | select name @tag kind")]
    [DataRow("$d | pick book | sort @tag desc")]
    public void AnAtWordRoundTrips(string source) => ParserHarness.AssertRoundTrips(source);

    /// <summary>The <c>@</c> commits, as the stop does: a name must follow it.</summary>
    [TestMethod]
    [DataRow("echo $v.@", 9)]
    [DataRow("echo $v.@ x", 9)]
    [DataRow("where $row.@ eq book", 12)]
    public void AnAtMustBeFollowedByAName(string source, int column)
    {
        var error = ParserHarness.ParseError(source);

        Assert.AreEqual(column, error.Column);
        Assert.AreEqual("a name belongs after the @, as in $v.@tag", error.Explanation);
    }

    /// <summary>A stop with nothing after it says what it did before, and asks for a column name alone.</summary>
    [TestMethod]
    public void AStopWithNoNameStillSaysAColumnNameBelongsThere()
    {
        var error = ParserHarness.ParseError("echo $v.");

        Assert.AreEqual("a column name belongs after the stop, as in $row.kind", error.Explanation);
        CollectionAssert.AreEqual(new[] { "column name" }, error.ExpectedSymbols.ToArray());
    }
}
