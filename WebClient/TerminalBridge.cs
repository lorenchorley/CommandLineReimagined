using System.Text.Json;
using CommandLineReimagined.Web;
using Microsoft.JSInterop;

namespace CommandLineReimagined.WebClient;

/// <summary>
/// The browser's entry point into a running terminal.
/// </summary>
/// <remarks>
/// <see cref="ParserBridge"/> only classifies text. This runs it: the same core the
/// desktop shell uses, against a log kept in the browser's own storage. The session is
/// static because a page is one terminal, and the location, the variables and the log
/// have to survive between calls -- and, since Phase 2, between visits.
///
/// Live output goes the other way: while a command runs, its output lines are pushed to
/// <c>window.terminal.output(id, lines)</c>. Pushes are coalesced to one every few tens
/// of milliseconds, because a download updates its counter on every 4 KB chunk and each
/// interop call costs more than the draw.
/// </remarks>
public static class TerminalBridge
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static IJSRuntime? _js;
    private static TerminalSession? _session;
    private static IndexedDbLog? _log;

    private static int _pendingId;
    private static IReadOnlyList<string>? _pendingLines;
    private static bool _flushScheduled;

    public static void Attach(IJSRuntime js) => _js = js;

    /// <summary>
    /// The session, once it has been initialised.
    /// </summary>
    /// <remarks>
    /// Not lazily constructed any more. Opening the store is asynchronous, so the
    /// session cannot be built on first use by a synchronous call; the page awaits
    /// <see cref="Initialize"/> before enabling the input, and everything else runs
    /// after that.
    /// </remarks>
    private static TerminalSession Session =>
        _session ?? throw new InvalidOperationException(
            "The terminal has not been initialised. The page must await Initialize first.");

    /// <summary>Runs a command line and returns the result as JSON.</summary>
    [JSInvokable]
    public static async Task<string> Execute(string source, int executionId)
    {
        var response = await Session.ExecuteAsync(source ?? string.Empty, executionId);

        // Anything still queued belongs to this execution and is superseded by the
        // final response, which carries the complete output.
        _pendingLines = null;

        return JsonSerializer.Serialize(response, Options);
    }

    /// <summary>
    /// Re-reads a line for a live listing, and returns the result as JSON.
    /// </summary>
    /// <remarks>
    /// The page asks for this when the store changes, to redraw the listing it is
    /// showing. It commits nothing and leaves no scrollback entry, and a line that would
    /// change something comes back as a fault: a refresh nobody typed is not allowed to
    /// write.
    /// </remarks>
    [JSInvokable]
    public static async Task<string> Refresh(string source)
    {
        var response = await Session.RefreshAsync(source ?? string.Empty);

        return JsonSerializer.Serialize(response, Options);
    }

    /// <summary>Stops the running command. Returns whether there was one.</summary>
    [JSInvokable]
    public static bool Cancel() => Session.Cancel();

    /// <summary>
    /// Opens the store, replays the log, and reports what it found.
    /// </summary>
    /// <remarks>
    /// The page awaits this before enabling the input, because an empty filesystem and
    /// a lost one look identical and it must not show one as the other.
    ///
    /// Storage being unavailable is not a failure: the session runs in memory and the
    /// answer says so, which is what the page turns into `not persisted`. The only way
    /// this throws is a defect.
    /// </remarks>
    [JSInvokable]
    public static async Task<string> Initialize()
    {
        if (_session is not null)
        {
            return Describe();
        }

        _log = await IndexedDbLog.OpenAsync(
            _js ?? throw new InvalidOperationException("The bridge was not attached to a JavaScript runtime."));

        var session = new TerminalSession(_log);
        session.OutputChanged += OnOutputChanged;
        session.StoreChanged += OnStoreChanged;
        _session = session;

        await session.InitializeAsync();

        return Describe();
    }

    /// <summary>Whether the log is still reaching storage.</summary>
    /// <remarks>
    /// The same answer <see cref="Initialize"/> gave, asked again. Storage can stop
    /// answering with the page open, and the log falls back to memory when it does;
    /// the page asks after every line so it can say so while it is still worth knowing.
    /// </remarks>
    [JSInvokable]
    public static string Status() => Describe();

    /// <summary>What the page needs to know about the restore.</summary>
    private static string Describe() =>
        JsonSerializer.Serialize(
            new StoreStatus(
                _log?.IsPersistent ?? false,
                _session?.ReplayedCount ?? 0,
                _log?.Unreadable ?? 0,
                _log?.Unavailable),
            Options);

    /// <summary>Whether the log is reaching storage, and what was replayed from it.</summary>
    /// <param name="Persistent">False in a private window, or once storage has stopped answering.</param>
    /// <param name="Replayed">How many transactions came back. Zero on a first visit.</param>
    /// <param name="Unreadable">Stored transactions this build could not decode. They are skipped.</param>
    /// <param name="Reason">Why storage is unavailable, when it is.</param>
    public sealed record StoreStatus(bool Persistent, int Replayed, int Unreadable, string? Reason);

    /// <summary>The commands available, for help and for completion.</summary>
    [JSInvokable]
    public static string Commands() => JsonSerializer.Serialize(Session.Commands, Options);

    /// <summary>
    /// Completions for the word at the cursor, and the signature it is in, as JSON.
    /// </summary>
    /// <remarks>
    /// A task, because completion may run the stages before the cursor (decision
    /// 0031). The page numbers its requests and drops any answer that is not the latest.
    /// </remarks>
    [JSInvokable]
    public static async Task<string> Complete(string text, int cursor) =>
        JsonSerializer.Serialize(await Session.CompleteAsync(text ?? string.Empty, cursor), Options);

    /// <summary>The variables currently in scope.</summary>
    [JSInvokable]
    public static string Variables() => JsonSerializer.Serialize(Session.Variables(), Options);

    /// <summary>Where the session is, for the prompt.</summary>
    [JSInvokable]
    public static string Location() => JsonSerializer.Serialize(Session.Location, Options);

    /// <summary>
    /// Tells the page that the store moved on.
    /// </summary>
    /// <remarks>
    /// Fire and forget, and deliberately so: this is raised from inside a commit, which
    /// is inside the interop call the page is still awaiting, so calling back
    /// synchronously would have the page asking for a refresh of a line that has not
    /// finished running. The page defers as well; between the two, a refresh always
    /// lands after the line that caused it.
    /// </remarks>
    private static void OnStoreChanged(long sequence) => _ = NotifyStoreChanged(sequence);

    private static async Task NotifyStoreChanged(long sequence)
    {
        try
        {
            if (_js is not null)
            {
                await _js.InvokeVoidAsync("terminal.storeChanged", sequence);
            }
        }
        catch (Exception)
        {
            // The page may have no handler yet, or may be unloading. A missed refresh
            // costs a stale table until the next line, which is not worth a failure.
        }
    }

    private static void OnOutputChanged(int executionId, IReadOnlyList<string> lines)
    {
        _pendingId = executionId;
        _pendingLines = lines;

        if (_flushScheduled)
        {
            return;
        }

        _flushScheduled = true;
        _ = FlushSoon();
    }

    private static async Task FlushSoon()
    {
        try
        {
            await Task.Delay(40);

            var lines = _pendingLines;
            _pendingLines = null;

            if (lines is not null && _js is not null)
            {
                await _js.InvokeVoidAsync("terminal.output", _pendingId, lines);
            }
        }
        catch (Exception)
        {
            // The page may have no handler yet, or may be unloading. Progress is
            // best-effort; the final response still carries every line.
        }
        finally
        {
            _flushScheduled = false;

            if (_pendingLines is not null)
            {
                _flushScheduled = true;
                _ = FlushSoon();
            }
        }
    }
}
