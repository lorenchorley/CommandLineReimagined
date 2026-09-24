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
        // Plain delegates: the core converts them. Building F# functions here with
        // FuncConvert put F# types in the adapter and, worse, compiled fine and then
        // failed in the browser, because the WebAssembly trimmer removes its generic
        // overloads.
        var options = SessionOptionsModule.ofDelegates(
            () => DateTimeOffset.UtcNow,
            () => Guid.NewGuid().ToString().ToLowerInvariant(),
            () => _httpClient,
            // The browser tab has nothing to close, so `exit` does nothing here. The
            // desktop host passes its own shutdown.
            () => { });

        _session = new Session(log ?? new InMemoryLog(), options, SessionOptionsModule.standardSeed(options));

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
    public async Task InitializeAsync()
    {
        await FSharpAsync.StartAsTask(
            _session.Initialize(),
            FSharpOption<TaskCreationOptions>.None,
            FSharpOption<CancellationToken>.None);

        // A log begun before the guide existed gets it now, once (decision 0036).
        await FSharpAsync.StartAsTask(
            _session.BringUpToDate(),
            FSharpOption<TaskCreationOptions>.None,
            FSharpOption<CancellationToken>.None);
    }

    /// <summary>
    /// How many transactions the log held when it was replayed.
    /// </summary>
    /// <remarks>
    /// Zero on a first visit. The page reports it, so that a restore which silently
    /// found nothing is visible rather than looking like a fresh session.
    /// </remarks>
    public int ReplayedCount => _session.ReplayedCount;

    public IReadOnlyList<CommandSummary> Commands =>
        _session.Commands
                .Select(spec => new CommandSummary(
                    spec.Name,
                    spec.Description,
                    spec.Parameters.Select(p => new ParameterSummary(p.Name, p.Optional)).ToList()))
                .ToList();

    /// <summary>Where the session is: a folder, and possibly a view over it.</summary>
    public LocationInfo Location => Describe(_session.Location);

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

        return Describe(source, response, parse);
    }

    /// <summary>
    /// Re-reads a line the way a live listing does.
    /// </summary>
    /// <remarks>
    /// The same response shape as <see cref="ExecuteAsync"/>, because the page draws the
    /// refreshed table with the same renderer it drew the first one with. What is
    /// different is underneath: nothing is committed, nothing reaches the history, and a
    /// line that would change something comes back as a fault instead of running.
    /// </remarks>
    public async Task<ExecutionResponse> RefreshAsync(string source)
    {
        source ??= string.Empty;

        var response = await FSharpAsync.StartAsTask(
            _session.Refresh(source),
            FSharpOption<TaskCreationOptions>.None,
            FSharpOption<CancellationToken>.None);

        return Describe(source, response, new CommandParseService().Parse(source));
    }

    /// <summary>One response, as the page's JSON.</summary>
    private static ExecutionResponse Describe(string source, Response response, ParseResponse parse)
    {
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
            Describe(response.Location),
            Describe(response.Changes),
            response.Guide?.Value is { IsEmpty: false } guide ? Describe(guide) : null,
            response.Notes.IsEmpty ? null : response.Notes.Select(Describe).ToList());
    }

    private static NoteInfo Describe(Note note) =>
        new(note.Kind, note.Text, NoteModule.fixLines(note).ToList());

    private static LogChangesInfo Describe(LogChanges changes) =>
        new(changes.Committed.ToList(), changes.Undone.ToList(), changes.Redone.ToList(), changes.Reset);

    /// <summary>
    /// Where the session is, as the prompt needs it.
    /// </summary>
    /// <remarks>
    /// A view travels as the text it was written as, because that is the whole of what
    /// the page does with it: shows it, and offers it back if you want to type it again.
    /// </remarks>
    private static LocationInfo Describe(Location location) =>
        new(location.Folder, location.View is null ? null : ExprModule.display(location.View.Value));

    public bool Cancel() => _session.Cancel();

    /// <summary>
    /// What the word at the cursor could become, and the signature it is in.
    /// </summary>
    /// <remarks>
    /// Asynchronous because completion may run the stages before the cursor to learn
    /// what flows into them (decision 0031). The page keeps only the answer to its
    /// latest keystroke.
    /// </remarks>
    public async Task<CompletionResponse> CompleteAsync(string text, int cursor)
    {
        text ??= string.Empty;
        cursor = Math.Clamp(cursor, 0, text.Length);

        var result = await FSharpAsync.StartAsTask(
            _session.Complete(text, cursor),
            FSharpOption<TaskCreationOptions>.None,
            FSharpOption<CancellationToken>.None);

        return new CompletionResponse(
            result.Items.Select(Describe).ToList(),
            result.Signature is null ? null : Describe(result.Signature.Value));
    }

    /// <summary>
    /// What the token that ends at <paramref name="offset"/> is, for the page's tap.
    /// </summary>
    /// <remarks>
    /// Null when there is nothing to say about it, and the page then shows the token's
    /// grammar role as it always did. Asynchronous because a <c>$row.</c> member asks
    /// what flows into its stage, which may run the stages before it (decision 0031).
    /// </remarks>
    public async Task<HoverInfo?> DescribeAsync(string text, int offset)
    {
        text ??= string.Empty;
        offset = Math.Clamp(offset, 0, text.Length);

        var result = await FSharpAsync.StartAsTask(
            _session.Describe(text, offset),
            FSharpOption<TaskCreationOptions>.None,
            FSharpOption<CancellationToken>.None);

        // An F# option is a reference whose None is null.
        if (result is null)
        {
            return null;
        }

        var hover = result.Value;

        return new HoverInfo(
            hover.Kind,
            hover.Text,
            hover.Detail?.Value,
            hover.Signature is null ? null : Describe(hover.Signature.Value));
    }

    /// <summary>What the end of the text could become, waited for.</summary>
    public IReadOnlyList<Completion> Complete(string text) =>
        _session.Complete(text ?? string.Empty).Select(Describe).ToList();

    private static Completion Describe(Core.Completion completion) =>
        new(completion.Kind,
            completion.Text,
            completion.Start,
            completion.End,
            completion.Detail is null ? null : completion.Detail.Value);

    private static SignatureInfo Describe(Signature signature) =>
        new(signature.Command,
            signature.Description,
            signature.Parameters
                     .Select(p => new SignatureParameter(p.Item1, p.Item2, p.Item3))
                     .ToList(),
            signature.Active is null ? null : signature.Active.Value);

    public IReadOnlyList<VariableSummary> Variables() =>
        _session.Variables()
                .Select(pair => new VariableSummary(
                    pair.Item1,
                    ValueModule.display(pair.Item2),
                    Describe(pair.Item2)))
                .ToList();

    /// <summary>What each label the parser expects reads as, in the order a list names them.</summary>
    /// <remarks>
    /// Phase 8. The parser names what could have come next by grammar labels:
    /// <c>identifier</c>, <c>&lt;$</c>, <c>{</c>. Those describe the grammar rather than
    /// the line, and this is the one place they become words. Several labels read the
    /// same (the three quotes are one quoted string), and a list names each phrase once.
    /// A null phrase is left out: <c>/</c> is only ever expected because a word could go
    /// on, which says nothing. A label that is not here is quoted as written, which is
    /// right for a keyword a person types and never shows a rule's name.
    /// </remarks>
    private static readonly (string Label, string? Phrase)[] ExpectedPhrases =
    {
        ("identifier", "a command name"),
        ("argument", "an argument"),
        ("$", "a variable"),
        ("variable name", "a variable name"),
        ("-", "a flag"),
        ("\"", "a quoted string"),
        ("\"\"", "a quoted string"),
        ("\"\"\"", "a quoted string"),
        ("(", "a parenthesised pipeline"),
        ("<", "a tag"),
        ("<$", "a tag"),
        ("{", "a component"),
        ("tag type", "a tag type"),
        ("attribute name", "an attribute name"),
        ("property name", "a property name"),
        ("column name", "a column name"),
        ("not", "'not'"),
        ("eq", "an operator"),
        ("ne", "an operator"),
        ("gt", "an operator"),
        ("ge", "an operator"),
        ("lt", "an operator"),
        ("le", "an operator"),
        ("like", "an operator"),
        ("has", "an operator"),
        ("and", "an operator"),
        ("or", "an operator"),
        ("try", "'try'"),
        ("??", "'??'"),
        ("else", "'else'"),
        ("|", "a pipe"),
        (",", "a comma"),
        (")", "a closing parenthesis"),
        ("]", "a closing bracket"),
        ("/>", "the end of the tag"),
        (">", "the end of the tag"),
        ("/}", "the end of the component"),
        ("}", "the end of the component"),
        ("</", "a closing tag"),
        ("{/", "a closing tag"),
        ("[/", "a closing tag"),
        ("/", null),
        ("end of input", "the end of the line"),
    };

    /// <summary>What the parser expected, as one list of phrases: <c>a tag or a component</c>.</summary>
    /// <remarks>
    /// In the table's order rather than the parser's, so the same error always reads
    /// the same way. Each phrase appears once, and the last two are joined with "or".
    /// </remarks>
    public static string DescribeExpected(IEnumerable<string> labels)
    {
        var written = new HashSet<string>(labels, StringComparer.Ordinal);
        var phrases = new List<string>();

        void Add(string? phrase)
        {
            if (phrase is not null && !phrases.Contains(phrase))
            {
                phrases.Add(phrase);
            }
        }

        foreach (var (label, phrase) in ExpectedPhrases)
        {
            if (written.Remove(label))
            {
                Add(phrase);
            }
        }

        foreach (var label in written.OrderBy(label => label, StringComparer.Ordinal))
        {
            Add($"'{label}'");
        }

        return phrases.Count switch
        {
            0 => string.Empty,
            1 => phrases[0],
            _ => $"{string.Join(", ", phrases.Take(phrases.Count - 1))} or {phrases[^1]}",
        };
    }

    /// <summary>
    /// Turns a parse failure into a sentence.
    /// </summary>
    /// <remarks>
    /// A mismatched closing tag arrives as a message rather than a position, and the
    /// old formatting rendered it as "error error at column 0", dropping the one part
    /// that said what was wrong. What was expected is said in words
    /// (<see cref="DescribeExpected"/>), never in the grammar's labels.
    /// </remarks>
    public static string Describe(ParseErrorInfo error)
    {
        // A rule that knew why the input was wrong said so, and that sentence beats any
        // list of what could have appeared there. The column still comes with it,
        // because the word it is about may not be the only one on the line.
        if (!string.IsNullOrEmpty(error.Explanation))
        {
            return $"Column {error.Column}: {error.Explanation}";
        }

        if (error.Kind is not ("syntax" or "lexical"))
        {
            return error.Expected.Count > 0
                ? string.Join(" ", error.Expected)
                : "Could not parse the command.";
        }

        string kind = error.Kind == "lexical" ? "Lexical" : "Syntax";
        string where = $"{kind} error at column {error.Column}";

        string expected = DescribeExpected(error.Expected);

        return expected.Length == 0
            ? where + "."
            : $"{where}: expected {expected}.";
    }

    // An F# option is a reference whose None is null, so the null-conditional reads
    // the two optional fields without a helper.
    private static FaultInfo Describe(Fault fault) =>
        new(FaultKindModule.name(fault.Kind), fault.Message, fault.Stage?.Value, fault.Path?.Value);

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

        // A table keeps its shape. Flattening it into chips would throw away exactly
        // what a table is for, and the page draws a real table from this.
        if (value is Value.Table table)
        {
            return new[] { Describe(table.Item) };
        }

        // A file keeps its path, so tapping the chip inserts something that resolves.
        if (value.IsFile)
        {
            return new[]
            {
                new ResultItem(ValueModule.kind(value), ValueModule.display(value), ValueModule.argument(value)),
            };
        }

        // A fault that `try` caught is a value, not an error, and the page draws it as
        // one; its kind travels beside the message so the page can label it.
        if (value is Value.Fault fault)
        {
            return new[]
            {
                new ResultItem("fault", fault.Item.Message, fault.Item.Path?.Value,
                    FaultKind: FaultKindModule.name(fault.Item.Kind)),
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

    /// <summary>
    /// A table as one item, with its columns and its cells.
    /// </summary>
    /// <remarks>
    /// Each cell is described the same way a standalone value is, so a file in a
    /// listing is still a chip that carries its path and a number is still a number.
    /// A cell that describes to several items — only a list does — is joined, because
    /// a cell is one cell.
    /// </remarks>
    private static ResultItem Describe(Table table)
    {
        var columns = table.Columns
            .Select(column => new ResultColumn(column.Name, ColumnTypeName(column.Type)))
            .ToList();

        var rows = table.Rows
            .Select(row => (IReadOnlyList<ResultItem>)row.Select(Cell).ToList())
            .ToList();

        return new ResultItem("table", ValueModule.display(Value.NewTable(table)), null, columns, rows);
    }

    private static ResultItem Cell(Value value) =>
        Describe(value) is [var single]
            ? single
            : new ResultItem(ValueModule.kind(value), ValueModule.display(value), null);

    private static string ColumnTypeName(ColumnType type) =>
        type.Tag switch
        {
            ColumnType.Tags.NumberCol => "number",
            ColumnType.Tags.BooleanCol => "boolean",
            ColumnType.Tags.FileCol => "file",
            ColumnType.Tags.ObjectCol => "object",
            ColumnType.Tags.MixedCol => "mixed",
            _ => "text",
        };
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
    LogChangesInfo Changes,
    // How to call the command when the line called it wrongly (decision 0038).
    IReadOnlyList<ResultItem>? Guide = null,
    // What the terminal says of its own about the line, drawn as guidance (0041 to 0044).
    IReadOnlyList<NoteInfo>? Notes = null);

/// <summary>Something the terminal says of its own about a line (decision 0041).</summary>
/// <remarks>
/// <paramref name="Kind"/> is <c>suggestion</c> or <c>explanation</c>. Each of
/// <paramref name="Fixes"/> is a whole corrected line, which the page offers as a chip
/// that fills the input without running it (decision 0044).
/// </remarks>
public sealed record NoteInfo(string Kind, string Text, IReadOnlyList<string> Fixes);

/// <summary>What a line did to the log, by the sequence numbers of the lines involved.</summary>
/// <remarks>
/// What lets the page make `undo` look like taking the line back: it remembers which
/// numbers each entry committed, hides the entry that <paramref name="Undone"/> names and
/// shows it again when <paramref name="Redone"/> does. A number is always the line's own,
/// never the undo's. <paramref name="Reset"/> says the numbers started again, so the ones
/// remembered from before it name nothing.
/// </remarks>
public sealed record LogChangesInfo(
    IReadOnlyList<long> Committed,
    IReadOnlyList<long> Undone,
    IReadOnlyList<long> Redone,
    bool Reset);

/// <summary>One thing the page draws.</summary>
/// <remarks>
/// <paramref name="Columns"/> and <paramref name="Rows"/> are set only on a table, and
/// are what lets the page draw a real one with sortable headers and tappable cells
/// instead of a run of chips. <paramref name="FaultKind"/> is set only on a fault that
/// <c>try</c> or <c>else</c> made into a value (Phase 5).
/// </remarks>
public sealed record ResultItem(
    string Kind,
    string Text,
    string? Path,
    IReadOnlyList<ResultColumn>? Columns = null,
    IReadOnlyList<IReadOnlyList<ResultItem>>? Rows = null,
    string? FaultKind = null);

/// <summary>A table's column, with the type its cells agreed on (decision 0009).</summary>
public sealed record ResultColumn(string Name, string Type);

/// <summary>What went wrong, with structure the page can act on.</summary>
/// <remarks>
/// `error` stays a sentence for the red line; this is beside it, so the page can show
/// the kind as a small tag and, later, so a script can match on it.
/// </remarks>
public sealed record FaultInfo(string Kind, string Message, int? Stage, string? Path);

/// <summary>
/// Where the session is: a folder, and the predicate being looked through, if any.
/// </summary>
/// <remarks>
/// Both at once, because a view does not replace the folder: new files still land in
/// <paramref name="Folder"/> while <paramref name="View"/> decides what a listing shows.
/// </remarks>
public sealed record LocationInfo(string Folder, string? View);

public sealed record CommandSummary(string Name, string Description, IReadOnlyList<ParameterSummary> Parameters);

public sealed record ParameterSummary(string Name, bool Optional);

/// <summary>
/// A possible continuation of the word at the cursor: replace the text from
/// <paramref name="Start"/> to <paramref name="End"/> with <paramref name="Text"/>.
/// </summary>
/// <param name="Detail">A line about it: what a variable holds, what a command does. Null when there is nothing to say.</param>
public sealed record Completion(string Kind, string Text, int Start, int End, string? Detail);

/// <summary>What the word could become, and the signature of the command it is in.</summary>
public sealed record CompletionResponse(IReadOnlyList<Completion> Items, SignatureInfo? Signature);

/// <summary>A command's parameters, with <paramref name="Active"/> the one being written.</summary>
public sealed record SignatureInfo(
    string Command, string Description, IReadOnlyList<SignatureParameter> Parameters, int? Active);

public sealed record SignatureParameter(string Name, bool Optional, string Description);

/// <summary>What a tapped token is.</summary>
/// <param name="Kind">`variable`, `command`, `member`, `operator`, `flag` or `argument`.</param>
/// <param name="Text">The token as written, sigils included.</param>
/// <param name="Detail">A variable's summary, a column's type, what an operator compares, a parameter's description.</param>
/// <param name="Signature">The command's parameters, for a command name or one of its arguments.</param>
public sealed record HoverInfo(string Kind, string Text, string? Detail, SignatureInfo? Signature);

public sealed record VariableSummary(string Name, string Text, IReadOnlyList<ResultItem> Items);
