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
}

public class LexicalError
{
    public SyntaxError SyntaxError { get; init; } = new();
}
