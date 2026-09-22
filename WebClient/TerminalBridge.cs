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
        _session = session;

        await session.InitializeAsync();

        return Describe();
    }

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

    /// <summary>Completions for the last word of the text.</summary>
    [JSInvokable]
    public static string Complete(string text) =>
        JsonSerializer.Serialize(Session.Complete(text ?? string.Empty), Options);

    /// <summary>The variables currently in scope.</summary>
    [JSInvokable]
    public static string Variables() => JsonSerializer.Serialize(Session.Variables(), Options);

    /// <summary>Where the session is, for the prompt.</summary>
    [JSInvokable]
    public static string Location() => JsonSerializer.Serialize(Session.Location, Options);

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
