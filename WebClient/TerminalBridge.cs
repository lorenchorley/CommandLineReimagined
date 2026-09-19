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
/// </remarks>
public static class TerminalBridge
{
    private static readonly Lazy<TerminalSession> Session = new(() => new TerminalSession());

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Runs a command line and returns the result as JSON.</summary>
    [JSInvokable]
    public static async Task<string> Execute(string source)
    {
        var response = await Session.Value.ExecuteAsync(source ?? string.Empty);
        return JsonSerializer.Serialize(response, Options);
    }

    /// <summary>Undoes the last command.</summary>
    [JSInvokable]
    public static string Undo() => JsonSerializer.Serialize(Session.Value.Undo(), Options);

    /// <summary>The commands available, for help and for completion.</summary>
    [JSInvokable]
    public static string Commands() => JsonSerializer.Serialize(Session.Value.Commands, Options);

    /// <summary>The current working directory, for the prompt.</summary>
    [JSInvokable]
    public static string WorkingDirectory() => Session.Value.WorkingDirectory;
}
