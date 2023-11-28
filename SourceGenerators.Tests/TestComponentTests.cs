using EntityComponentSystem;
using EntityComponentSystem.EventSourcing;
using System.Text;

namespace SourceGenerators.Tests;

[TestClass]
public class TestComponentTests
{
    [TestClass]
    public class Test_AnotherClass
    {
    }
}

public class TestComponent : Component
{
    public virtual StringBuilder StringBuilder { get; set; }
    public virtual StringBuilder? StringBuilderP { get; set; }
    public virtual bool Highlighted { get; set; }
    public virtual bool? HighlightedP { get; set; }
    public virtual int Int { get; set; }
    public virtual int? IntP { get; set; }
    public virtual string Text { get; set; }
    public virtual string? TextP { get; set; }
    public virtual double Double { get; set; }
    public virtual double? DoubleP { get; set; }
    public virtual float Float { get; set; }
    public virtual float? FloatP { get; set; }
    public virtual object Object { get; set; }
    public virtual object? ObjectP { get; set; }

}

