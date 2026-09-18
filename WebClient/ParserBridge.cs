using System.Text.Json;
using CommandLineReimagined.Web.Parsing;
using Microsoft.JSInterop;

namespace CommandLineReimagined.WebClient;

/// <summary>
/// The browser's entry point into the parser.
/// </summary>
/// <remarks>
/// Returns the same JSON shape as the ASP.NET host's /api/parse, so the canvas client
/// is identical in both deployments: it either calls this or fetches that, and cannot
/// tell the difference. The server and this client share
/// <see cref="CommandParseService"/>, so the token stream is the same code path too.
/// </remarks>
public static class ParserBridge
{
    private static readonly CommandParseService Parser = new();

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [JSInvokable]
    public static string Parse(string source) =>
        JsonSerializer.Serialize(Parser.Parse(source ?? string.Empty), Options);
}
