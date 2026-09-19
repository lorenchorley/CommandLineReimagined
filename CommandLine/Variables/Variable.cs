using Terminal.Execution;

namespace Terminal.Variables;

/// <summary>
/// A named value in a <see cref="Terminal.Scoping.Scope"/>.
/// </summary>
/// <remarks>
/// Previously both properties were get-only with no constructor, so no instance could
/// ever hold anything. It now carries a <see cref="RuntimeValue"/>, which is what
/// <c>&lt;name|type/&gt;</c> binds and what <c>$name</c> reads back.
/// </remarks>
public class Variable
{
    public Variable(string name, RuntimeValue value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public RuntimeValue Value { get; }
}
