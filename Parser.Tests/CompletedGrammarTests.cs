using Commands.Parser.SemanticTree;

namespace Parser.Tests;

/// <summary>
/// The parts of the grammar that the table-driven parser accepted but could never build
/// a tree for.
/// </summary>
/// <remarks>
/// Each of these was pinned by a test asserting NotImplementedException before the
/// migration. They are assertions about what the language does now.
/// </remarks>
[TestClass]
public class CompletedGrammarTests
{
    // <FunctionArgumentList> ::= <FunctionArgumentList> ',' <FunctionArgument>

    [TestMethod]
    public void FunctionTakesSeveralArguments()
    {
        var arguments = ParserHarness.Function("command(a, b, c)").Arguments.Arguments;

        Assert.AreEqual(3, arguments.Count);
        CollectionAssert.AllItemsAreInstancesOfType(arguments.ToList(), typeof(RequiredCommandArgument));
    }

    [TestMethod]
    public void FunctionMixesRequiredAndNamedArguments()
    {
        var arguments = ParserHarness.Function("command(a, name: value)").Arguments.Arguments;

        Assert.AreEqual(2, arguments.Count);
        Assert.IsInstanceOfType(arguments[0], typeof(RequiredCommandArgument));
        Assert.IsInstanceOfType(arguments[1], typeof(OptionalCommandArgument));
    }

    [TestMethod]
    public void FunctionArgumentsMayBeStrings()
    {
        var arguments = ParserHarness.Function("command(\"x\", y: \"z\")").Arguments.Arguments;

        Assert.AreEqual("x", ((StringConstant)((RequiredCommandArgument)arguments[0]).Value).Value);
        Assert.AreEqual("z", ((StringConstant)((OptionalCommandArgument)arguments[1]).Value).Value);
    }

    // <Value> ::= <InstanceTag>

    [TestMethod]
    public void CommandTakesATagAsAnArgument()
    {
        var argument = (CommandArgumentValue)ParserHarness.Cli("command <thing/>").Arguments.Arguments[0];
        var tag = (TagValue)argument.Value;

        Assert.AreEqual("thing", ((ObjectInstance)tag.Tag).ObjectType.Value);
    }

    [TestMethod]
    public void FunctionTakesATagAsAnArgument()
    {
        var argument = (RequiredCommandArgument)ParserHarness.Function("command(<thing/>)").Arguments.Arguments[0];

        Assert.IsInstanceOfType(argument.Value, typeof(TagValue));
    }

    // <ComponentInstance>, previously unreachable in every form

    [TestMethod]
    public void ClosedComponentHasATypeAndNoChildren()
    {
        var component = Component("{renderer/}");

        Assert.AreEqual("renderer", component.ComponentType.Value);
        Assert.IsFalse(component.HasChildren);
    }

    [TestMethod]
    public void ComponentCarriesAttributes()
    {
        var component = Component("{renderer colour=red/}");

        Assert.AreEqual(1, component.Attributes.Attributes.Count);
        Assert.AreEqual("colour", component.Attributes.Attributes[0].Name.Name);
    }

    [TestMethod]
    public void OpenComponentClosesWithOrWithoutItsType()
    {
        Assert.AreEqual("renderer", Component("{renderer}{/}").ComponentType.Value);
        Assert.AreEqual("renderer", Component("{renderer}{/renderer}").ComponentType.Value);
    }

    [TestMethod]
    public void ComponentMayBindAVariable() =>
        Assert.AreEqual("handle", Component("{handle|renderer}{/}").VariableName!.Name);

    [TestMethod]
    public void ComponentNestsInsideAnObject()
    {
        var instance = ParserHarness.Instance("<entity>{renderer/}</entity>");

        Assert.AreEqual(1, instance.Children!.Tags.Count);
        Assert.IsInstanceOfType(instance.Children.Tags[0], typeof(ComponentInstance));
    }

    // <PropertyAssignment>, the forms that hold tags rather than a simple value

    [TestMethod]
    public void PropertyHoldsASimpleValue()
    {
        var property = Property("<t>[size=3]</t>");

        Assert.AreEqual("size", property.Name.Name);
        Assert.AreEqual("3", ((Identifier)property.Value!).Name);
        Assert.IsFalse(property.HasChildren);
    }

    [TestMethod]
    public void PropertyHoldsASingleTag()
    {
        var property = Property("<t>[child]=<x/></t>");

        Assert.IsTrue(property.HasChildren);
        Assert.AreEqual(1, property.Children!.Tags.Count);
    }

    [TestMethod]
    public void PropertyHoldsAListOfTags()
    {
        var property = Property("<t>[children]<a/><b/>[/children]</t>");

        Assert.AreEqual(2, property.Children!.Tags.Count);
    }

    [TestMethod]
    public void PropertyListMayCloseWithoutItsName() =>
        Assert.AreEqual(1, Property("<t>[children]<a/>[/]</t>").Children!.Tags.Count);

    // <VariableTag> ::= '<' '$' <VariableName> '>'

    [TestMethod]
    public void VariableTagIsACommandOnItsOwn()
    {
        var expression = ParserHarness.SingleCommand("<$handle>").Expression;

        Assert.IsTrue(expression.IsT2);
        Assert.AreEqual("handle", ((VariableTag)expression.AsT2).Name.Name);
    }

    [TestMethod]
    public void VariableTagSerialises() => ParserHarness.AssertRoundTrips("<$handle>");

    // StringLiteral4, which <Constant> never referred to

    [TestMethod]
    public void TripleQuotedStringIsAConstant()
    {
        var argument = (CommandArgumentValue)ParserHarness.Cli("echo \"\"\"triple\"\"\"").Arguments.Arguments[0];

        Assert.AreEqual("triple", ((StringConstant)argument.Value).Value);
    }

    private static ComponentInstance Component(string source)
    {
        var expression = ParserHarness.SingleCommand(source).Expression;
        Assert.IsTrue(expression.IsT2, $"'{source}' is not an instance tag.");

        return expression.AsT2 as ComponentInstance
            ?? throw new AssertFailedException($"'{source}' is not a component instance.");
    }

    private static PropertyAssignment Property(string source)
    {
        var instance = ParserHarness.Instance(source);

        return instance.Children!.Tags.OfType<PropertyAssignment>().FirstOrDefault()
            ?? throw new AssertFailedException($"'{source}' contains no property assignment.");
    }
}
