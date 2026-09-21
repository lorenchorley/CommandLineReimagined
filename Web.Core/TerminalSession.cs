using System.Net.Http;
using CommandLineReimagined.Core;
using CommandLineReimagined.Web.Parsing;
using CommandLineReimagined.Web.Tokenisation;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;

namespace CommandLineReimagined.Web;

/// <summary>
/// One terminal, as JSON.
/// </summary>
/// <remarks>
/// An adapter and nothing more. Every decision about what a command means lives in
/// <see cref="Session"/>, which is F#; this turns its values into the shapes the page
/// already knows how to draw, and turns the page's strings into calls. The rule from
/// the architecture is that C# never sees a <c>Value</c> or an <c>Outcome</c>: they
/// stop here.
/// </remarks>
public sealed class TerminalSession
{
    private readonly Session _session;
    private readonly HttpClient _httpClient = new();

    public TerminalSession(ILog? log = null)
    {
        var options = new SessionOptions(
            clock: FuncConvert.FromFunc<DateTimeOffset>(() => DateTimeOffset.UtcNow),
            newId: FuncConvert.FromFunc(() => Guid.NewGuid().ToString().ToLowerInvariant()),
            httpClient: FuncConvert.FromFunc(() => _httpClient),
            // The browser tab has nothing to close, so `exit` does nothing here. The
            // desktop host passes its own shutdown.
            exit: FuncConvert.FromAction(() => { }));

        _session = new Session(
            log ?? new InMemoryLog(),
            options,
            SeedModule.standard(options.NewId, options.Clock));

        _session.OutputChanged.AddHandler(
            new FSharpHandler<Tuple<int, FSharpList<string>>>((_, args) =>
                OutputChanged?.Invoke(args.Item1, args.Item2.ToList())));

        _session.StoreChanged.AddHandler(
            new FSharpHandler<long>((_, sequence) => StoreChanged?.Invoke(sequence)));
    }

    /// <summary>
    /// Replays the log before anything can run.
    /// </summary>
    /// <remarks>
    /// Separate from the constructor because replaying is asynchronous: in Phase 2 it
    /// reads IndexedDB, and WebAssembly has no thread to block. Executing before this
    /// has completed is a fault rather than an empty filesystem, so a page that forgets
    /// to await it says so instead of quietly losing the user's files.
    /// </remarks>
    public Task InitializeAsync() => FSharpAsync.StartAsTask(
        _session.Initialize(),
        FSharpOption<TaskCreationOptions>.None,
        FSharpOption<CancellationToken>.None);

    public IReadOnlyList<CommandSummary> Commands =>
        _session.Commands
                .Select(spec => new CommandSummary(
                    spec.Name,
                    spec.Description,
                    spec.Parameters.Select(p => new ParameterSummary(p.Name, p.Optional)).ToList()))
                .ToList();

    /// <summary>Where the session is: a folder, and from Phase 4 possibly a view.</summary>
    public LocationInfo Location => new(_session.Location.Folder, null);

    /// <summary>
    /// The folder path, under the name the page used before there were views.
    /// </summary>
    /// <remarks>
    /// Kept for one phase so the page and this can be updated separately, then removed.
    /// </remarks>
    [Obsolete("Use Location. Removed after Phase 2.")]
    public string WorkingDirectory => _session.Location.Folder;

    public bool IsRunning => _session.IsRunning;

    /// <summary>
    /// Raised while a command runs, with the execution id and the complete lines so far.
    /// </summary>
    public event Action<int, IReadOnlyList<string>>? OutputChanged;

    /// <summary>Raised after every committed transaction. Phase 4's live views use it.</summary>
    public event Action<long>? StoreChanged;

    public async Task<ExecutionResponse> ExecuteAsync(
        string source, int executionId = 0, CancellationToken cancellation = default)
    {
        source ??= string.Empty;

        // Tokens come from a separate parse: the page colours what was typed even when
        // it does not run, and the session's own parse failure is a fault, not tokens.
        var parse = new CommandParseService().Parse(source);

        var response = await FSharpAsync.StartAsTask(
            _session.Execute(source, executionId, cancellation),
            FSharpOption<TaskCreationOptions>.None,
            FSharpOption<CancellationToken>.None);

        var fault = response.Fault is null ? null : Describe(response.Fault.Value);

        // A parse failure carries a column and a list of what would have been accepted,
        // which the fault does not. Where the session says the line would not parse,
        // the parser's own sentence is the better one to show.
        string? error = fault is null
            ? null
            : fault.Kind == nameof(FaultKind.Syntax) && parse.Error is not null
                ? Describe(parse.Error)
                : fault.Message;

        var value = response.Result?.Value;
        var result = value is null || value.IsEmpty ? null : Describe(value);

        return new ExecutionResponse(
            "result",
            source,
            parse.Tokens,
            response.Output.ToList(),
            result,
            value is null ? null : ValueModule.display(value),
            error,
            fault,
            new LocationInfo(response.Location.Folder, null),
            response.Location.Folder);
    }

    public bool Cancel() => _session.Cancel();

    public IReadOnlyList<Completion> Complete(string text) =>
        _session.Complete(text ?? string.Empty)
                .Select(c => new Completion(c.Kind, c.Text, c.Start))
                .ToList();

    public IReadOnlyList<VariableSummary> Variables() =>
        _session.Variables()
                .Select(pair => new VariableSummary(
                    pair.Item1,
                    ValueModule.display(pair.Item2),
                    Describe(pair.Item2)))
                .ToList();

    /// <summary>
    /// Turns a parse failure into a sentence.
    /// </summary>
    /// <remarks>
    /// A mismatched closing tag arrives as a message rather than a position, and the
    /// old formatting rendered it as "error error at column 0", dropping the one part
    /// that said what was wrong.
    /// </remarks>
    private static string Describe(ParseErrorInfo error)
    {
        if (error.Kind is not ("syntax" or "lexical"))
        {
            return error.Expected.Count > 0
                ? string.Join(" ", error.Expected)
                : "Could not parse the command.";
        }

        string kind = error.Kind == "lexical" ? "Lexical" : "Syntax";
        string where = $"{kind} error at column {error.Column}";

        return error.Expected.Count == 0
            ? where + "."
            : $"{where}: expected {string.Join(", ", error.Expected)}.";
    }

    // An F# option is a reference whose None is null, so the null-conditional reads
    // the two optional fields without a helper.
    private static FaultInfo Describe(Fault fault) =>
        new(fault.Kind.ToString(), fault.Message, fault.Stage?.Value, fault.Path?.Value);

    /// <summary>
    /// Flattens a value into items the page can draw.
    /// </summary>
    /// <remarks>
    /// A file keeps its kind and its path, so the page can draw it as the tappable chip
    /// the desktop app draws as a button, and so tapping it inserts something that
    /// resolves. A list flattens: `ls` is a row of chips, not one chip saying "list".
    /// </remarks>
    private static IReadOnlyList<ResultItem> Describe(Value value)
    {
        if (value.IsEmpty)
        {
            return Array.Empty<ResultItem>();
        }

        if (value is Value.List list)
        {
            return list.Item.SelectMany(Describe).ToList();
        }

        // A file keeps its path, so tapping the chip inserts something that resolves.
        if (value.IsFile)
        {
            return new[]
            {
                new ResultItem(ValueModule.kind(value), ValueModule.display(value), ValueModule.argument(value)),
            };
        }

        return new[]
        {
            new ResultItem(
                ValueModule.kind(value),
                ValueModule.display(value),
                null),
        };
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
    FaultInfo? Fault,
    LocationInfo Location,
    string WorkingDirectory);

public sealed record ResultItem(string Kind, string Text, string? Path);

/// <summary>What went wrong, with structure the page can act on.</summary>
/// <remarks>
/// `error` stays a sentence for the red line; this is beside it, so the page can show
/// the kind as a small tag and, later, so a script can match on it.
/// </remarks>
public sealed record FaultInfo(string Kind, string Message, int? Stage, string? Path);

/// <summary>Where the session is. `View` is a saved query, and arrives in Phase 4.</summary>
public sealed record LocationInfo(string Folder, string? View);

public sealed record CommandSummary(string Name, string Description, IReadOnlyList<ParameterSummary> Parameters);

public sealed record ParameterSummary(string Name, bool Optional);

/// <summary>A possible continuation of the last word: replace the text from <paramref name="Start"/> with <paramref name="Text"/>.</summary>
public sealed record Completion(string Kind, string Text, int Start);

public sealed record VariableSummary(string Name, string Text, IReadOnlyList<ResultItem> Items);
