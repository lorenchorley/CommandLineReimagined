using Commands;
using Commands.Implementations;
using Commands.Parser;
using Commands.Parser.SemanticTree;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Execution;
using Terminal.Scoping;

namespace Execution.Tests;

/// <summary>
/// Parses and executes a command line without a scene.
/// </summary>
/// <remarks>
/// The execution layer depends on <see cref="ICommandOutput"/> rather than on CliBlock,
/// so these tests need no ECS, no window and no GDI+. Every command is registered,
/// including the async ones, now that none of them needs a render loop to construct.
/// </remarks>
public sealed class TestHarness
{
    private readonly CommandLineReimagined.Parsing.CommandLineParser _interpreter = new();
    private readonly ServiceProvider _services;

    public TestHarness(string workingDirectory)
    {
        var collection = new ServiceCollection();

        collection.AddSingleton<ScopeRegistry>();
        collection.AddSingleton(new TestPathModule(workingDirectory).Module);
        collection.AddSingleton<IApplicationLifetime, NoOpApplicationLifetime>();
        collection.AddHttpClient();

        Register<ListDirectoryContents>(collection);
        Register<ChangeDirectory>(collection);
        Register<UpOneDirectory>(collection);
        Register<PrintWorkingDirectory>(collection);
        Register<MakeDirectory>(collection);
        Register<CopyFile>(collection);
        Register<ReadFile>(collection);
        Register<WriteFile>(collection);
        Register<Remove>(collection);
        Register<Echo>(collection);
        Register<SetVariable>(collection);
        Register<ListVariables>(collection);
        Register<ProgressTest>(collection);
        Register<Download>(collection);
        Register<Exit>(collection);
        Register<UnknownCommand>(collection);

        _services = collection.BuildServiceProvider();

        History = new CommandHistory();
        Evaluator = new CommandEvaluator(
            _services,
            _services.GetServices<ICommandAction>().Select(a => a.Profile),
            History);

        Scope = _services.GetRequiredService<ScopeRegistry>().Global;
    }

    public CommandEvaluator Evaluator { get; }

    public CommandHistory History { get; }

    public Scope Scope { get; }

    public RecordingOutput Output { get; } = new();

    public CommandLine.Modules.PathModule PathModule =>
        _services.GetRequiredService<CommandLine.Modules.PathModule>();

    /// <summary>Parses and runs a line, returning the pipeline's value.</summary>
    public RuntimeValue Run(string commandLine) => RunAsync(commandLine).GetAwaiter().GetResult();

    public Task<RuntimeValue> RunAsync(string commandLine, CancellationToken cancellation = default)
    {
        var tree = Parse(commandLine);
        return Evaluator.ExecuteAsync(tree, Output, Scope, cancellation);
    }

    /// <summary>Runs a line expected to fail, returning the error message.</summary>
    public string RunExpectingError(string commandLine)
    {
        try
        {
            Run(commandLine);
        }
        catch (ConsoleError error)
        {
            return error.Message;
        }

        throw new AssertFailedException($"Expected '{commandLine}' to raise a ConsoleError.");
    }

    public RootNode Parse(string commandLine)
    {
        var result = _interpreter.Parse<RootNode>(commandLine);

        return result.Match(
            tree => tree,
            errors => throw new AssertFailedException(
                $"'{commandLine}' did not parse: {string.Join(", ", Describe(errors))}"));
    }

    private static IEnumerable<string> Describe(ParserError error) =>
        error.Match(
            messages => messages,
            syntax => new List<string> { $"syntax error at column {syntax.Column}" },
            lexical => new List<string> { $"lexical error at column {lexical.SyntaxError.Column}" });

    private static void Register<TCommand>(IServiceCollection collection)
        where TCommand : class, ICommandAction
    {
        // Singleton rather than transient so a test can assert on the same instance the
        // command ran on. The real app uses transient; undo still works there because
        // the history holds the instance that executed.
        collection.AddSingleton<TCommand>();
        collection.AddSingleton<ICommandAction>(sp => sp.GetRequiredService<TCommand>());
    }
}

/// <summary>
/// PathModule reads its starting directory from a hard-coded constant, so this points it
/// at a temporary directory instead.
/// </summary>
public sealed class TestPathModule
{
    public TestPathModule(string workingDirectory)
    {
        Module = new CommandLine.Modules.PathModule(new ScopeRegistry());
        Module.MoveTo(workingDirectory);
    }

    public CommandLine.Modules.PathModule Module { get; }
}

/// <summary>Captures what commands write while running.</summary>
public sealed class RecordingOutput : ICommandOutput, IClearableOutput
{
    private readonly List<RecordingLine> _lines = new();

    public IReadOnlyList<string> Written =>
        _lines.SelectMany(l => l.Texts).Select(t => t.Text).ToList();

    public bool Cleared { get; private set; }

    public IOutputLine NewLine()
    {
        var line = new RecordingLine();
        _lines.Add(line);
        return line;
    }

    public void AbandonLine(IOutputLine line)
    {
        if (line is RecordingLine recording)
        {
            _lines.Remove(recording);
        }
    }

    public void Clear()
    {
        Cleared = true;
        _lines.Clear();
    }

    private sealed class RecordingLine : IOutputLine
    {
        public List<RecordingText> Texts { get; } = new();

        public IOutputText Write(string description, string text)
        {
            var written = new RecordingText { Text = text };
            Texts.Add(written);
            return written;
        }
    }

    private sealed class RecordingText : IOutputText
    {
        public string Text { get; set; } = string.Empty;
    }
}
