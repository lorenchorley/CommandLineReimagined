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
    public StringBuilder StringBuilder { get; set; }
    public StringBuilder? StringBuilderP { get; set; }
    public bool Highlighted { get; set; }
    public bool? HighlightedP { get; set; }
    public int Int { get; set; }
    public int? IntP { get; set; }
    public string Text { get; set; }
    public string? TextP { get; set; }
    public double Double { get; set; }
    public double? DoubleP { get; set; }
    public float Float { get; set; }
    public float? FloatP { get; set; }
    public object Object { get; set; }
    public object? ObjectP { get; set; }

}

