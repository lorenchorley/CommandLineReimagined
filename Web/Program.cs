using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// App Service for Linux hands the port to the app via PORT. The built-in .NET stack
// usually sets ASPNETCORE_URLS as well, but honouring PORT explicitly keeps this
// working under a plain container too.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

app.MapGet("/healthz", () => Results.Ok(new
{
    status = "ok",
    utc = DateTime.UtcNow,
    runtime = Environment.Version.ToString(),
}));

// Echo endpoint. This is a placeholder for the ECS delta stream, but it earns its
// keep now: App Service requires WebSockets to be switched on explicitly at the
// site level, and this is what proves the provisioning step actually did that.
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Expected a WebSocket upgrade request.");
        return;
    }

    using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
    var buffer = new byte[4 * 1024];

    var hello = JsonSerializer.Serialize(new { type = "hello", utc = DateTime.UtcNow });
    await socket.SendAsync(Encoding.UTF8.GetBytes(hello), WebSocketMessageType.Text, true, context.RequestAborted);

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

        if (result.MessageType == WebSocketMessageType.Close)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            break;
        }

        var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
        var reply = JsonSerializer.Serialize(new { type = "echo", text });
        await socket.SendAsync(Encoding.UTF8.GetBytes(reply), WebSocketMessageType.Text, true, context.RequestAborted);
    }
});

app.Run();
