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
/// closes.
///
/// Long-running commands report as they go: whatever they write to their output is
/// raised through <see cref="OutputChanged"/> while they run, and <see cref="Cancel"/>
/// stops the one in flight. Both exist so the browser can show a progress bar moving
/// and a Stop button that works, which is how async execution gets tested by hand.
/// </remarks>
public sealed class TerminalSession
{
    private readonly ServiceProvider _services;
    private readonly CommandEvaluator _evaluator;
    private readonly Scope _scope;
    private readonly PathModule _pathModule;

    private CancellationTokenSource? _running;

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

    /// <summary>Whether a command is currently executing.</summary>
    public bool IsRunning => _running is { IsCancellationRequested: false };

    /// <summary>
    /// Raised whenever a running command changes its output. Carries the execution id
    /// passed to <see cref="ExecuteAsync"/> and the complete current lines, so a listener
    /// can redraw without tracking deltas.
    /// </summary>
    public event Action<int, IReadOnlyList<string>>? OutputChanged;

    /// <summary>Parses and runs a command line.</summary>
    public async Task<ExecutionResponse> ExecuteAsync(
        string source, int executionId = 0, CancellationToken cancellation = default)
    {
        source ??= string.Empty;
        var parse = new CommandParseService().Parse(source);

        if (parse.Error is not null)
        {
            return new ExecutionResponse(
                "result", source, parse.Tokens, Array.Empty<string>(),
                null, null, $"{parse.Error.Kind} error at column {parse.Error.Column}", WorkingDirectory);
        }

        if (IsRunning)
        {
            return new ExecutionResponse(
                "result", source, parse.Tokens, Array.Empty<string>(),
                null, null, "A command is already running. Stop it first.", WorkingDirectory);
        }

        var output = new CapturingOutput(lines => OutputChanged?.Invoke(executionId, lines));
        var running = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        _running = running;

        try
        {
            var tree = ParseTree(source);
            var value = await _evaluator.ExecuteAsync(tree, output, _scope, running.Token);

            return new ExecutionResponse(
                "result", source, parse.Tokens, output.Lines,
                Describe(value), value.ToDisplayString(), null, WorkingDirectory);
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResponse(
                "result", source, parse.Tokens, output.Lines,
                null, null, "Stopped.", WorkingDirectory);
        }
        catch (ConsoleError error)
        {
            return new ExecutionResponse(
                "result", source, parse.Tokens, output.Lines,
                null, null, error.Message, WorkingDirectory);
        }
        catch (Exception exception)
        {
            return new ExecutionResponse(
                "result", source, parse.Tokens, output.Lines,
                null, null, $"{exception.GetType().Name} : {exception.Message}", WorkingDirectory);
        }
        finally
        {
            if (ReferenceEquals(_running, running))
            {
                _running = null;
            }

            running.Dispose();
        }
    }

    /// <summary>Stops the command in flight, if there is one.</summary>
    public bool Cancel()
    {
        var running = _running;

        if (running is null || running.IsCancellationRequested)
        {
            return false;
        }

        running.Cancel();
        return true;
    }

    /// <summary>
    /// What the text could continue as: a command name at the start of the line, a
    /// variable after a <c>$</c>, and otherwise a file or directory relative to the
    /// current one.
    /// </summary>
    /// <remarks>
    /// Returns whole replacements for the last word rather than suffixes, so the client
    /// can show them as chips and substitute one on a tap. Phone keyboards have no Tab
    /// key, so this is the only completion most users will have.
    /// </remarks>
    public IReadOnlyList<Completion> Complete(string text)
    {
        text ??= string.Empty;

        int wordStart = LastWordStart(text);
        string word = text[wordStart..];
        bool firstWord = text[..wordStart].Trim().Length == 0 || text[..wordStart].TrimEnd().EndsWith('|');

        var completions = new List<Completion>();

        // Nothing typed yet: offering every command is noise, and the page shows its
        // suggestion chips in that state instead.
        if (firstWord && word.Length == 0)
        {
            return completions;
        }

        if (word.StartsWith('$'))
        {
            string prefix = word[1..];

            foreach (var variable in _scope.AllVariables())
            {
                if (variable.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(new Completion("variable", "$" + variable.Name, wordStart));
                }
            }

            return completions;
        }

        if (firstWord)
        {
            foreach (var command in Commands)
            {
                if (command.Name.StartsWith(word, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(new Completion("command", command.Name, wordStart));
                }
            }

            // Words the page handles itself, kept here so one list drives completion.
            foreach (var builtin in new[] { "help", "clear", "undo" })
            {
                if (builtin.StartsWith(word, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(new Completion("command", builtin, wordStart));
                }
            }

            if (completions.Count > 0)
            {
                return completions;
            }
        }

        // Paths: the part before the last separator says which directory to look in.
        int separator = Math.Max(word.LastIndexOf('/'), word.LastIndexOf('\\'));
        string directoryPart = separator >= 0 ? word[..(separator + 1)] : string.Empty;
        string namePart = separator >= 0 ? word[(separator + 1)..] : word;

        string directory;
        try
        {
            directory = directoryPart.Length == 0
                ? _pathModule.CurrentPath
                : _pathModule.Resolve(directoryPart);
        }
        catch (Exception)
        {
            return completions;
        }

        if (!Directory.Exists(directory))
        {
            return completions;
        }

        foreach (var entry in Directory.EnumerateDirectories(directory).OrderBy(e => e, StringComparer.Ordinal))
        {
            string name = Path.GetFileName(entry);
            if (name.StartsWith(namePart, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(new Completion("directory", directoryPart + name + "/", wordStart));
            }
        }

        foreach (var entry in Directory.EnumerateFiles(directory).OrderBy(e => e, StringComparer.Ordinal))
        {
            string name = Path.GetFileName(entry);
            if (name.StartsWith(namePart, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(new Completion("file", directoryPart + name, wordStart));
            }
        }

        return completions;
    }

    /// <summary>The variables in scope, for the client to show.</summary>
    public IReadOnlyList<VariableSummary> Variables() =>
        _scope.AllVariables()
              .Select(v => new VariableSummary(v.Name, v.Value.ToDisplayString(), Describe(v.Value)))
              .ToList();

    private static int LastWordStart(string text)
    {
        int i = text.Length;

        while (i > 0 && !char.IsWhiteSpace(text[i - 1]) && text[i - 1] != '|' && text[i - 1] != '(' && text[i - 1] != ',')
        {
            i--;
        }

        return i;
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
            ComponentValue component => new[] { new ResultItem("component", component.ToDisplayString(), null) },
            NumberValue number => new[] { new ResultItem("number", number.ToDisplayString(), null) },
            BooleanValue boolean => new[] { new ResultItem("boolean", boolean.ToDisplayString(), null) },
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

    /// <summary>
    /// Collects what a command writes while it runs, and says so each time it changes.
    /// </summary>
    /// <remarks>
    /// Each line keeps its written segments separately, so a command that writes a label
    /// and then keeps updating a progress figure after it changes only the figure.
    /// </remarks>
    private sealed class CapturingOutput : ICommandOutput, IClearableOutput
    {
        private readonly List<CapturedLine> _lines = new();
        private readonly Action<IReadOnlyList<string>> _changed;

        public CapturingOutput(Action<IReadOnlyList<string>> changed)
        {
            _changed = changed;
        }

        public IReadOnlyList<string> Lines =>
            _lines.Select(line => line.Text).Where(text => text.Length > 0).ToList();

        public IOutputLine NewLine()
        {
            var line = new CapturedLine(Notify);
            _lines.Add(line);
            return line;
        }

        public void AbandonLine(IOutputLine line)
        {
            if (line is CapturedLine captured && _lines.Remove(captured))
            {
                Notify();
            }
        }

        public void Clear()
        {
            _lines.Clear();
            Notify();
        }

        private void Notify() => _changed(Lines);

        private sealed class CapturedLine : IOutputLine
        {
            private readonly List<CapturedText> _segments = new();
            private readonly Action _changed;

            public CapturedLine(Action changed) => _changed = changed;

            public string Text => string.Concat(_segments.Select(s => s.Text));

            public IOutputText Write(string description, string text)
            {
                var segment = new CapturedText(text, _changed);
                _segments.Add(segment);
                _changed();
                return segment;
            }
        }

        private sealed class CapturedText : IOutputText
        {
            private readonly Action _changed;
            private string _text;

            public CapturedText(string text, Action changed)
            {
                _text = text;
                _changed = changed;
            }

            public string Text
            {
                get => _text;
                set
                {
                    if (_text != value)
                    {
                        _text = value;
                        _changed();
                    }
                }
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

/// <summary>A possible continuation of the last word: replace the text from <paramref name="Start"/> with <paramref name="Text"/>.</summary>
public sealed record Completion(string Kind, string Text, int Start);

public sealed record VariableSummary(string Name, string Text, IReadOnlyList<ResultItem> Items);
