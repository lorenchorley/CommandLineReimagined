using CommandLineReimagined.Web.Tokenisation;
using Commands.Parser;
using Commands.Parser.SemanticTree;
using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace CommandLineReimagined.Web.Parsing;

public sealed record ParseResponse(
    string Type,
    string Source,
    IReadOnlyList<SemanticToken> Tokens,
    string? Reserialised,
    ParseErrorInfo? Error);

public sealed record ParseErrorInfo(string Kind, int Line, int Column, IReadOnlyList<string> Expected);

/// <summary>
/// Wraps the GOLD-engine interpreter for the web host.
/// </summary>
/// <remarks>
/// <see cref="CommandLineInterpreter"/> builds two LALR parsers from embedded .egt
/// tables in its constructor, which is not cheap, so it is held per process. The
/// GOLD parser carries mutable position state across a parse, so calls are serialised
/// behind a lock rather than allowing concurrent connections to interleave.
/// </remarks>
public sealed class CommandParseService
{
    private readonly CommandLineInterpreter _interpreter = new();
    private readonly object _lock = new();

    public ParseResponse Parse(string source)
    {
        lock (_lock)
        {
            ParserResult<RootNode> result = _interpreter.Parse<RootNode>(source);

            return result.Match(
                tree =>
                {
                    var tokeniser = new TokenStreamVisitor();
                    tree.Accept(tokeniser);

                    // Round-tripping through the serialiser proves the tree is faithful:
                    // if this differs from the source, the parse lost information.
                    var serialiser = new SerialisationVisitor();
                    tree.Accept(serialiser);

                    return new ParseResponse("tokens", source, tokeniser.Tokens, serialiser.GetResult(), null);
                },
                error => new ParseResponse("tokens", source, Array.Empty<SemanticToken>(), null, Describe(error)));
        }
    }

    private static ParseErrorInfo Describe(ParserError error) =>
        error.Match(
            messages => new ParseErrorInfo("error", 0, 0, messages),
            syntax => new ParseErrorInfo("syntax", syntax.Line, syntax.Column, Expected(syntax)),
            lexical => new ParseErrorInfo("lexical", lexical.SyntaxError.Line, lexical.SyntaxError.Column, Expected(lexical.SyntaxError)));

    private static IReadOnlyList<string> Expected(SyntaxError syntax)
    {
        var symbols = syntax.ExpectedSymbols;
        if (symbols is null)
        {
            return Array.Empty<string>();
        }

        // GOLD's SymbolList exposes Count() as a method, not a property.
        int count = symbols.Count();
        var expected = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            expected.Add(symbols[i].ToString());
        }

        return expected;
    }
}
