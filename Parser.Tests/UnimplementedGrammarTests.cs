namespace Parser.Tests;

/// <summary>
/// Grammar the language defines but the current parser cannot build a tree for.
/// </summary>
/// <remarks>
/// CommandLineGrammar.grm accepts all of these; CommandLineInterpreter has no case for
/// the production and throws NotImplementedException, or the tree builds and the
/// serialiser has no case for the node. They are pinned here rather than left silent so
/// the gap list is executable: each test names the production it is waiting on, and a
/// parser that implements the rule will fail the test and can then be asserted properly.
/// </remarks>
[TestClass]
public class UnimplementedGrammarTests
{
    /// <summary>&lt;FunctionArgumentList&gt; ::= &lt;FunctionArgumentList&gt; ',' &lt;FunctionArgument&gt;</summary>
    [DataTestMethod]
    [DataRow("command(a, b)")]
    [DataRow("command(a, b, c)")]
    [DataRow("command(a, name: value)")]
    [DataRow("command(\"x\", y: \"z\")")]
    public void FunctionCallsTakeOnlyOneArgument(string source)
    {
        // Only the single-argument production is implemented, so a comma is fatal. This
        // is the most user-visible of the gaps: function notation is documented in the
        // grammar's own comments as the equivalent of flags.
        Assert.IsInstanceOfType(ParserHarness.ParseThrows(source), typeof(NotImplementedException));
    }

    /// <summary>&lt;Value&gt; ::= &lt;InstanceTag&gt;</summary>
    [DataTestMethod]
    [DataRow("command <thing/>")]
    [DataRow("command(<thing/>)")]
    public void ATagCannotBeUsedAsAValue(string source)
    {
        // A tag is accepted as a whole command (<IndividualCLIValue>) but not as an
        // argument to one, so a command cannot receive a constructed object.
        Assert.IsInstanceOfType(ParserHarness.ParseThrows(source), typeof(NotImplementedException));
    }

    /// <summary>&lt;ComponentInstance&gt;, in every form</summary>
    [DataTestMethod]
    [DataRow("{component/}")]
    [DataRow("{component a=1/}")]
    [DataRow("{component}{/}")]
    [DataRow("{component}{/component}")]
    [DataRow("{handle|component}{/}")]
    public void ComponentInstancesAreNotBuilt(string source)
    {
        // <ComponentType>, <InstanceTag> ::= <ComponentInstance> and <Tag> ::=
        // <ComponentInstance> all throw. The brace syntax parses but no tree exists for
        // it, so the ECS component half of the language is unreachable.
        Assert.IsInstanceOfType(ParserHarness.ParseThrows(source), typeof(NotImplementedException));
    }

    /// <summary>&lt;InstanceTagList&gt;, used by the tag-list forms of &lt;PropertyAssignment&gt;</summary>
    [DataTestMethod]
    [DataRow("<t>[p]<x/>[/p]</t>")]
    [DataRow("<t>[p]<x/>[/]</t>")]
    [DataRow("<t>[p]=<x/></t>")]
    public void PropertiesCannotHoldTags(string source)
    {
        // `[name=value]` works, but the forms that give a property a tag or a list of
        // tags do not, so a property can only ever hold a simple value.
        Assert.IsInstanceOfType(ParserHarness.ParseThrows(source), typeof(NotImplementedException));
    }

    /// <summary>&lt;VariableTag&gt; ::= '&lt;' '$' &lt;VariableName&gt; '&gt;'</summary>
    [TestMethod]
    public void VariableTagCannotBecomeACommand()
    {
        // The production itself is implemented and builds a VariableTag, and VariableTag
        // derives from InstanceTag, so the grammar's <IndividualCLIValue> ::= <InstanceTag>
        // should accept it. ConvertToCommandExpression tests `value is ObjectInstance`
        // instead of `value is InstanceTag`, so a VariableTag falls past every branch and
        // hits the generic throw. VisitorBase.VisitVariableTag is also unimplemented, so
        // even a tree built by hand could not be serialised.
        var thrown = ParserHarness.ParseThrows("<$handle>");

        Assert.AreEqual("Unknown expression type", thrown.Message);
    }
}
