using Commands.Parser.SemanticTree;

namespace Parser.Tests;

/// <summary>
/// Object instance tags: the closed and open forms, variable binding, attributes and
/// nesting.
/// </summary>
[TestClass]
public class ObjectInstanceTests
{
    // <ClosedFormObjectInstance> ::= '<' <ObjectType> <TagAttributeList> '/' '>'

    [TestMethod]
    public void ClosedTagHasATypeAndNoChildren()
    {
        var instance = ParserHarness.Instance("<thing/>");

        Assert.AreEqual("thing", instance.ObjectType.Value);
        Assert.IsFalse(instance.HasChildren);
        Assert.IsNull(instance.VariableName);
    }

    [TestMethod]
    public void ClosedTagCarriesOneAttribute()
    {
        var attributes = ParserHarness.Instance("<thing size=large/>").Attributes.Attributes;

        Assert.AreEqual(1, attributes.Count);
        Assert.AreEqual("size", attributes[0].Name.Name);
        Assert.AreEqual("large", ((Identifier)attributes[0].Value).Name);
    }

    [TestMethod]
    public void ClosedTagCarriesSeveralAttributes()
    {
        var attributes = ParserHarness.Instance("<thing a=1 b=two c=3/>").Attributes.Attributes;

        Assert.AreEqual(3, attributes.Count);
        CollectionAssert.AreEqual(
            new[] { "a", "b", "c" },
            attributes.Select(a => a.Name.Name).ToArray());
    }

    // <TagAttribute> ::= <TagAttributeName> '=' <SimpleValue>

    [TestMethod]
    public void AttributeValueMayBeAString() =>
        Assert.AreEqual(
            "a value",
            ((StringConstant)ParserHarness.Instance("<thing label=\"a value\"/>").Attributes.Attributes[0].Value).Value);

    [TestMethod]
    public void AttributeValueMayBeAVariableReference() =>
        Assert.AreEqual(
            "source",
            ((VariableReference)ParserHarness.Instance("<thing from=$source/>").Attributes.Attributes[0].Value).Name.Name);

    [TestMethod]
    public void AttributeRequiresAValue() => ParserHarness.ParseError("<thing broken=/>");

    // '<' <VariableName> '|' <ObjectType> <TagAttributeList> '/' '>'

    [TestMethod]
    public void ClosedTagMayBindAVariable()
    {
        var instance = ParserHarness.Instance("<handle|thing/>");

        Assert.AreEqual("handle", instance.VariableName!.Name);
        Assert.AreEqual("thing", instance.ObjectType.Value);
    }

    [TestMethod]
    public void VariableBindingCombinesWithAttributes()
    {
        var instance = ParserHarness.Instance("<handle|thing size=large/>");

        Assert.AreEqual("handle", instance.VariableName!.Name);
        Assert.AreEqual(1, instance.Attributes.Attributes.Count);
    }

    // <OpenFormObjectInstance> ::= <OpeningObjectTag> <TagList> <ClosingObjectTag>

    [TestMethod]
    public void OpenTagWithMatchingCloseHasNoChildren()
    {
        var instance = ParserHarness.Instance("<thing></thing>");

        Assert.AreEqual("thing", instance.ObjectType.Value);
        Assert.IsFalse(instance.HasChildren);
    }

    [TestMethod]
    public void ClosingTagMayOmitTheType() =>
        Assert.AreEqual("thing", ParserHarness.Instance("<thing></>").ObjectType.Value);

    [TestMethod]
    public void OpenTagMayBindAVariable() =>
        Assert.AreEqual("handle", ParserHarness.Instance("<handle|thing></>").VariableName!.Name);

    [TestMethod]
    public void NestedTagBecomesAChild()
    {
        var instance = ParserHarness.Instance("<outer><inner/></outer>");

        Assert.IsTrue(instance.HasChildren);
        Assert.AreEqual(1, instance.Children!.Tags.Count);
        Assert.AreEqual("inner", ((ObjectInstance)instance.Children.Tags[0]).ObjectType.Value);
    }

    [TestMethod]
    public void SeveralChildrenKeepTheirOrder()
    {
        var children = ParserHarness.Instance("<outer><a/><b/><c/></outer>").Children!.Tags;

        Assert.AreEqual(3, children.Count);
        CollectionAssert.AreEqual(
            new[] { "a", "b", "c" },
            children.Cast<ObjectInstance>().Select(t => t.ObjectType.Value).ToArray());
    }

    [TestMethod]
    public void ChildrenNestArbitrarilyDeep()
    {
        var outer = ParserHarness.Instance("<a><b><c/></b></a>");
        var middle = (ObjectInstance)outer.Children!.Tags[0];
        var inner = (ObjectInstance)middle.Children!.Tags[0];

        Assert.AreEqual("c", inner.ObjectType.Value);
    }

    [TestMethod]
    public void MismatchedClosingTagIsReported()
    {
        // The interpreter records this as a message rather than a syntax error, because
        // the shape is grammatically valid and only the names disagree.
        var thrown = Record(() => ParserHarness.Parse("<opened></different>"));

        Assert.IsNotNull(thrown, "A mismatched closing tag should not parse cleanly.");
    }

    [TestMethod]
    public void UnclosedTagIsRejected() => ParserHarness.ParseError("<thing");

    [TestMethod]
    public void TagWithoutATypeIsRejected() => ParserHarness.ParseError("</>");

    private static Exception? Record(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
