using OneOf;

namespace Commands.Parser;

[GenerateOneOf]
public partial class ParserError : OneOfBase<List<string>, SyntaxError, LexicalError>
{
}

/// <summary>
/// Where a command line stopped making sense, and what could have appeared there.
/// </summary>
/// <remarks>
/// ExpectedSymbols was a GOLD.SymbolList, which tied every consumer of an error to the
/// GOLD engine. It is a list of names now, so the type describes a syntax error rather
/// than one particular parser's syntax error.
/// </remarks>
public class SyntaxError
{
    public int Line { get; init; }

    public int Column { get; init; }

    public IReadOnlyList<string> ExpectedSymbols { get; init; } = Array.Empty<string>();

    /// <summary>
    /// What the parser had to say about the position, when it had something better than
    /// a list of what could have appeared there.
    /// </summary>
    /// <remarks>
    /// A reserved word in argument position is the case this exists for: "expected /"
    /// says nothing, and "'eq' is an operator; write "eq" to pass it as text" says all
    /// of it. Null when the parser only knew what it was expecting.
    /// </remarks>
    public string? Explanation { get; init; }
}

public class LexicalError
{
    public SyntaxError SyntaxError { get; init; } = new();
}
