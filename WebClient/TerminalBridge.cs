using System.Text.Json;
using CommandLineReimagined.Web;
using Microsoft.JSInterop;

namespace CommandLineReimagined.WebClient;

/// <summary>
/// The browser's entry point into a running terminal.
/// </summary>
/// <remarks>
/// <see cref="ParserBridge"/> only classifies text. This runs it: the same evaluator,
/// commands and scope the desktop shell uses, against the browser's in-memory
/// filesystem. The session is static because a page is one terminal, and the working
/// directory and command history have to survive between calls.
///
/// Live output goes the other way: while a command runs, its output lines are pushed to
/// <c>window.terminal.output(id, lines)</c>. Pushes are coalesced to one every few tens
/// of milliseconds, because a download updates its counter on every 4 KB chunk and each
/// interop call costs more than the draw.
/// </remarks>
public static class TerminalBridge
{
    private static readonly Lazy<TerminalSession> Session = new(CreateSession);

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static IJSRuntime? _js;

    private static int _pendingId;
    private static IReadOnlyList<string>? _pendingLines;
    private static bool _flushScheduled;

    public static void Attach(IJSRuntime js) => _js = js;

    /// <summary>Runs a command line and returns the result as JSON.</summary>
    [JSInvokable]
    public static async Task<string> Execute(string source, int executionId)
    {
        var response = await Session.Value.ExecuteAsync(source ?? string.Empty, executionId);

        // Anything still queued belongs to this execution and is superseded by the
        // final response, which carries the complete output.
        _pendingLines = null;

        return JsonSerializer.Serialize(response, Options);
    }

    /// <summary>Stops the running command. Returns whether there was one.</summary>
    [JSInvokable]
    public static bool Cancel() => Session.Value.Cancel();

    /// <summary>Undoes the last command.</summary>
    [JSInvokable]
    public static string Undo() => JsonSerializer.Serialize(Session.Value.Undo(), Options);

    /// <summary>The commands available, for help and for completion.</summary>
    [JSInvokable]
    public static string Commands() => JsonSerializer.Serialize(Session.Value.Commands, Options);

    /// <summary>Completions for the last word of the text.</summary>
    [JSInvokable]
    public static string Complete(string text) =>
        JsonSerializer.Serialize(Session.Value.Complete(text ?? string.Empty), Options);

    /// <summary>The variables currently in scope.</summary>
    [JSInvokable]
    public static string Variables() => JsonSerializer.Serialize(Session.Value.Variables(), Options);

    /// <summary>The current working directory, for the prompt.</summary>
    [JSInvokable]
    public static string WorkingDirectory() => Session.Value.WorkingDirectory;

    private static TerminalSession CreateSession()
    {
        var session = new TerminalSession();
        session.OutputChanged += OnOutputChanged;
        return session;
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
