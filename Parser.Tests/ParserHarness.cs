using Commands.Parser;
using Commands.Parser.SemanticTree;
using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Parser.Tests;

/// <summary>
/// Drives the parser directly, so tests can tell the three failure modes apart.
/// </summary>
/// <remarks>
/// A command line can fail in ways that look the same from outside: the grammar can
/// reject it, the interpreter can lack a production for a rule the grammar accepts, or
/// the tree can parse but fail to serialise. These are separate defects with separate
/// fixes, so the harness keeps them separate.
/// </remarks>
public static class ParserHarness
{
    // The suite runs against the FParsec parser. The GOLD interpreter is still built
    // and is exercised by ParserEquivalenceTests, which runs both over the same corpus.
    private static readonly CommandLineReimagined.Parsing.CommandLineParser Interpreter = new();
    private static readonly object Gate = new();

    public static RootNode Parse(string source)
    {
        lock (Gate)
        {
            var result = Interpreter.Parse<RootNode>(source);

            return result.Match(
                tree => tree,
                error => throw new AssertFailedException(
                    $"'{source}' did not parse: {Describe(error)}"));
        }
    }

    /// <summary>The syntax error for a source the grammar rejects.</summary>
    public static SyntaxError ParseError(string source)
    {
        lock (Gate)
        {
            var result = Interpreter.Parse<RootNode>(source);

            return result.Match(
                tree => throw new AssertFailedException($"'{source}' parsed, but a syntax error was expected."),
                error => error.Match(
                    messages => throw new AssertFailedException(
                        $"'{source}' failed with messages rather than a syntax error: {string.Join(", ", messages)}"),
                    syntax => syntax,
                    lexical => lexical.SyntaxError));
        }
    }

    /// <summary>
    /// The exception raised while building the tree, for grammar the interpreter has no
    /// production for.
    /// </summary>
    public static Exception ParseThrows(string source)
    {
        lock (Gate)
        {
            try
            {
                Interpreter.Parse<RootNode>(source);
            }
            catch (Exception exception)
            {
                return exception;
            }

            throw new AssertFailedException($"'{source}' parsed without throwing.");
        }
    }

    public static string Serialise(RootNode tree)
    {
        var visitor = new SerialisationVisitor();
        tree.Accept(visitor);
        return visitor.GetResult();
    }

    /// <summary>The exception raised while serialising a tree that parsed successfully.</summary>
    public static Exception SerialiseThrows(string source)
    {
        var tree = Parse(source);

        try
        {
            Serialise(tree);
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new AssertFailedException($"'{source}' serialised without throwing.");
    }

    public static string RoundTrip(string source) => Serialise(Parse(source));

    /// <summary>Asserts that parsing and reserialising returns the original text.</summary>
    public static void AssertRoundTrips(string source) =>
        Assert.AreEqual(source, RoundTrip(source), $"'{source}' did not survive a round trip.");

    /// <summary>The single command in a one-command program.</summary>
    public static CommandExpression SingleCommand(string source)
    {
        var tree = Parse(source);
        var pipeline = tree as PipedCommandList
            ?? throw new AssertFailedException($"'{source}' produced {tree.GetType().Name}, not a PipedCommandList.");

        Assert.AreEqual(1, pipeline.OrderedCommands.Count, $"'{source}' produced more than one command.");
        return pipeline.OrderedCommands[0];
    }

    public static CommandExpressionCli Cli(string source)
    {
        var expression = SingleCommand(source).Expression;
        Assert.IsTrue(expression.IsT1, $"'{source}' is not in CLI notation.");
        return expression.AsT1;
    }

    public static FunctionExpression Function(string source)
    {
        var expression = SingleCommand(source).Expression;
        Assert.IsTrue(expression.IsT0, $"'{source}' is not a function expression.");
        return expression.AsT0;
    }

    public static ObjectInstance Instance(string source)
    {
        var expression = SingleCommand(source).Expression;
        Assert.IsTrue(expression.IsT2, $"'{source}' is not an instance tag.");
        return expression.AsT2 as ObjectInstance
            ?? throw new AssertFailedException($"'{source}' is not an object instance.");
    }

    private static string Describe(ParserError error) =>
        error.Match(
            messages => string.Join(", ", messages),
            syntax => $"syntax error at line {syntax.Line}, column {syntax.Column}",
            lexical => $"lexical error at line {lexical.SyntaxError.Line}, column {lexical.SyntaxError.Column}");
}
