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

/// <summary>Why a line would not parse.</summary>
/// <remarks>
/// <paramref name="Explanation"/> is what the grammar said when it knew why, as opposed
/// to what it was expecting. A reserved word in argument position is the case it exists
/// for; it is null when the parser only knew what could have appeared there.
/// </remarks>
public sealed record ParseErrorInfo(
    string Kind, int Line, int Column, IReadOnlyList<string> Expected, string? Explanation = null)
{
    /// <summary>The sentence running the line would show, worded by <see cref="TerminalSession.Describe(ParseErrorInfo)"/>.</summary>
    /// <remarks>
    /// Carried with the parse so the page's detail line says, while the line is being
    /// typed, exactly what running it would say, rather than keeping a second copy of
    /// the wording in JavaScript.
    /// </remarks>
    public string? Sentence { get; init; }
}

/// <summary>
/// Wraps the parser for the web host.
/// </summary>
/// <remarks>
/// The FParsec parser is immutable once built and its combinators carry no shared
/// state, so concurrent connections could parse in parallel. The lock is kept because
/// it costs nothing here and keeps this class safe if a stateful parser is ever
/// swapped back in.
/// </remarks>
public sealed class CommandParseService
{
    private readonly CommandLineReimagined.Parsing.CommandLineParser _interpreter = new();
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
                error =>
                {
                    var info = Describe(error);
                    return new ParseResponse(
                        "tokens", source, Array.Empty<SemanticToken>(), null,
                        info with { Sentence = TerminalSession.Describe(info) });
                });
        }
    }

    private static ParseErrorInfo Describe(ParserError error) =>
        error.Match(
            messages => new ParseErrorInfo("error", 0, 0, messages),
            syntax => new ParseErrorInfo("syntax", syntax.Line, syntax.Column, Expected(syntax), syntax.Explanation),
            lexical => new ParseErrorInfo(
                "lexical",
                lexical.SyntaxError.Line,
                lexical.SyntaxError.Column,
                Expected(lexical.SyntaxError),
                lexical.SyntaxError.Explanation));

    private static IReadOnlyList<string> Expected(SyntaxError syntax)
    {
        var symbols = syntax.ExpectedSymbols;
        if (symbols is null)
        {
            return Array.Empty<string>();
        }

        return symbols;
    }
}
