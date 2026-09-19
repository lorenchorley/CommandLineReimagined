using System.Text.Json.Serialization;
using CommandLine.Modules;
using Commands;
using Commands.Implementations;
using Commands.Parser.SemanticTree;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Execution;
using Terminal.Scoping;
using CommandLineReimagined.Web.Tokenisation;
using CommandLineReimagined.Web.Parsing;

namespace CommandLineReimagined.Web;

/// <summary>
/// One terminal: a command registry, a scope and a working directory, kept across
/// commands.
/// </summary>
/// <remarks>
/// The same <see cref="CommandEvaluator"/> the desktop shell uses. Nothing here is
/// web-specific except the output sink and the starting directory, which is what
/// depending on <see cref="ICommandOutput"/> rather than on the ECS bought.
///
/// Under WebAssembly the filesystem is Emscripten's in-memory one, so `mkdir` and `ls`
/// operate on a real directory tree that lives in the browser tab and disappears when it
/// closes. Commands that need the render loop (download, progress) are not registered,
/// because constructing them reaches GDI+.
/// </remarks>
public sealed class TerminalSession
{
    private readonly ServiceProvider _services;
    private readonly CommandEvaluator _evaluator;
    private readonly Scope _scope;
    private readonly PathModule _pathModule;

    public TerminalSession(string? rootDirectory = null)
    {
        string root = rootDirectory ?? DefaultRoot();
        Seed(root);

        var collection = new ServiceCollection();

        var scopes = new ScopeRegistry();
        collection.AddSingleton(scopes);

        var paths = new PathModule(scopes);
        paths.MoveTo(root);
        collection.AddSingleton(paths);

        collection.AddSingleton<IApplicationLifetime, NoOpApplicationLifetime>();

        Register<ListDirectoryContents>(collection);
        Register<ChangeDirectory>(collection);
        Register<MakeDirectory>(collection);
        Register<CopyFile>(collection);
        Register<UpOneDirectory>(collection);
        Register<Echo>(collection);
        Register<Exit>(collection);
        Register<UnknownCommand>(collection);

        _services = collection.BuildServiceProvider();
        _pathModule = paths;
        _scope = scopes.Global;

        History = new CommandHistory();
        _evaluator = new CommandEvaluator(
            _services,
            _services.GetServices<ICommandAction>().Select(action => action.Profile),
            History);

        Commands = _services.GetServices<ICommandAction>()
                            .Select(action => action.Profile)
                            .Where(profile => !string.Equals(profile.Name, "UnknownCommand", StringComparison.Ordinal))
                            .OrderBy(profile => profile.Name, StringComparer.Ordinal)
                            .Select(profile => new CommandSummary(
                                profile.Name,
                                profile.Description,
                                profile.Parameters.Select(p => new ParameterSummary(p.Name, p.IsOptional)).ToList()))
                            .ToList();
    }

    public CommandHistory History { get; }

    public IReadOnlyList<CommandSummary> Commands { get; }

    public string WorkingDirectory => _pathModule.CurrentPath;

    /// <summary>Parses and runs a command line.</summary>
    public async Task<ExecutionResponse> ExecuteAsync(string source, CancellationToken cancellation = default)
    {
        var parse = new CommandParseService().Parse(source ?? string.Empty);

        if (parse.Error is not null)
        {
            return new ExecutionResponse(
                "result", source ?? string.Empty, parse.Tokens, Array.Empty<string>(),
                null, null, $"{parse.Error.Kind} error at column {parse.Error.Column}", WorkingDirectory);
        }

        var output = new CapturingOutput();

        try
        {
            var tree = ParseTree(source ?? string.Empty);
            var value = await _evaluator.ExecuteAsync(tree, output, _scope, cancellation);

            return new ExecutionResponse(
                "result", source ?? string.Empty, parse.Tokens, output.Lines,
                Describe(value), value.ToDisplayString(), null, WorkingDirectory);
        }
        catch (ConsoleError error)
        {
            return new ExecutionResponse(
                "result", source ?? string.Empty, parse.Tokens, output.Lines,
                null, null, error.Message, WorkingDirectory);
        }
        catch (Exception exception)
        {
            return new ExecutionResponse(
                "result", source ?? string.Empty, parse.Tokens, output.Lines,
                null, null, $"{exception.GetType().Name} : {exception.Message}", WorkingDirectory);
        }
    }

    /// <summary>Undoes the last command, the way the desktop shell's undo key does.</summary>
    public ExecutionResponse Undo()
    {
        if (History.Count == 0)
        {
            return new ExecutionResponse(
                "result", "undo", Array.Empty<SemanticToken>(), Array.Empty<string>(),
                null, null, "Nothing to undo.", WorkingDirectory);
        }

        History.UndoLast();

        return new ExecutionResponse(
            "result", "undo", Array.Empty<SemanticToken>(), new[] { "Undone." },
            null, null, null, WorkingDirectory);
    }

    private static RootNode ParseTree(string source)
    {
        var parser = new CommandLineReimagined.Parsing.CommandLineParser();

        return parser.Parse<RootNode>(source).Match(
            tree => tree,
            error => throw new ConsoleError("Could not parse the command."));
    }

    /// <summary>
    /// Flattens a result into something the browser can render: paths keep their kind so
    /// the client can draw them as the interactive chips the desktop app draws as buttons.
    /// </summary>
    private static IReadOnlyList<ResultItem> Describe(RuntimeValue value) =>
        value switch
        {
            EmptyValue => Array.Empty<ResultItem>(),
            ListValue list => list.Items.SelectMany(Describe).ToList(),
            PathValue path => new[] { new ResultItem(path.Kind.ToString().ToLowerInvariant(), path.Name, path.Path) },
            ObjectValue instance => new[] { new ResultItem("object", instance.ToDisplayString(), null) },
            _ => new[] { new ResultItem("text", value.ToDisplayString(), null) },
        };

    private static string DefaultRoot() =>
        OperatingSystem.IsBrowser() ? "/home/terminal" : Path.Combine(Path.GetTempPath(), "clr-web");

    /// <summary>
    /// Gives a new session something to look at. An empty filesystem makes `ls` look
    /// broken rather than empty.
    /// </summary>
    private static void Seed(string root)
    {
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "documents"));
        Directory.CreateDirectory(Path.Combine(root, "projects"));

        string readme = Path.Combine(root, "readme.txt");
        if (!File.Exists(readme))
        {
            File.WriteAllText(readme, "This filesystem lives in the browser tab.");
        }

        string notes = Path.Combine(root, "documents", "notes.txt");
        if (!File.Exists(notes))
        {
            File.WriteAllText(notes, "Try: ls, cd documents, mkdir scratch, echo \"hello\"");
        }
    }

    private static void Register<TCommand>(IServiceCollection collection)
        where TCommand : class, ICommandAction
    {
        collection.AddSingleton<TCommand>();
        collection.AddSingleton<ICommandAction>(sp => sp.GetRequiredService<TCommand>());
    }

    /// <summary>Collects what a command writes while it runs.</summary>
    private sealed class CapturingOutput : ICommandOutput, IClearableOutput
    {
        private readonly List<CapturedLine> _lines = new();

        public IReadOnlyList<string> Lines =>
            _lines.Select(line => line.Text).Where(text => text.Length > 0).ToList();

        public IOutputLine NewLine()
        {
            var line = new CapturedLine();
            _lines.Add(line);
            return line;
        }

        public void AbandonLine(IOutputLine line)
        {
            if (line is CapturedLine captured)
            {
                _lines.Remove(captured);
            }
        }

        public void Clear() => _lines.Clear();

        private sealed class CapturedLine : IOutputLine, IOutputText
        {
            public string Text { get; set; } = string.Empty;

            public IOutputText Write(string description, string text)
            {
                Text += text;
                return this;
            }
        }
    }
}

public sealed record ExecutionResponse(
    string Type,
    string Source,
    IReadOnlyList<SemanticToken> Tokens,
    IReadOnlyList<string> Output,
    IReadOnlyList<ResultItem>? Result,
    string? ResultText,
    string? Error,
    string WorkingDirectory);

public sealed record ResultItem(string Kind, string Text, string? Path);

public sealed record CommandSummary(string Name, string Description, IReadOnlyList<ParameterSummary> Parameters);

public sealed record ParameterSummary(string Name, bool Optional);
