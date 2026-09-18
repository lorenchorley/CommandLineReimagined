using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CommandLineReimagined.Web.Parsing;

var builder = WebApplication.CreateBuilder(args);

// App Service for Linux hands the port to the app via PORT. The built-in .NET stack
// usually sets ASPNETCORE_URLS as well, but honouring PORT explicitly keeps this
// working under a plain container too.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

builder.Services.AddSingleton<CommandParseService>();

var app = builder.Build();

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

app.MapGet("/healthz", () => Results.Ok(new
{
    status = "ok",
    utc = DateTime.UtcNow,
    runtime = Environment.Version.ToString(),
}));

// Convenience for curl and for the CI smoke test; the live client uses the socket.
app.MapGet("/api/parse", (string? q, CommandParseService parser) =>
    Results.Json(parser.Parse(q ?? string.Empty), json));

app.Map("/ws", async (HttpContext context, CommandParseService parser) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Expected a WebSocket upgrade request.");
        return;
    }

    using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
    var buffer = new byte[16 * 1024];

    async Task SendAsync(object payload) =>
        await socket.SendAsync(
            JsonSerializer.SerializeToUtf8Bytes(payload, json),
            WebSocketMessageType.Text,
            endOfMessage: true,
            context.RequestAborted);

    await SendAsync(new { type = "hello", utc = DateTime.UtcNow });

    while (socket.State == WebSocketState.Open)
    {
        WebSocketReceiveResult result;
        try
        {
            result = await socket.ReceiveAsync(buffer, context.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (WebSocketException)
        {
            break;
        }

        if (result.MessageType == WebSocketMessageType.Close)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            break;
        }

        var raw = Encoding.UTF8.GetString(buffer, 0, result.Count);

        try
        {
            var request = JsonSerializer.Deserialize<ParseRequest>(raw, json);
            await SendAsync(parser.Parse(request?.Text ?? string.Empty));
        }
        catch (JsonException)
        {
            await SendAsync(new { type = "error", message = "Malformed request." });
        }
    }
});

app.Run();

internal sealed record ParseRequest(string? Type, string? Text);

// Exposed so a future test project can drive the host with WebApplicationFactory.
public partial class Program;
