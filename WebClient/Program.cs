using CommandLineReimagined.WebClient;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// No Blazor component tree: the UI is plain HTML in wwwroot/index.html. This host
// exists to boot the .NET runtime so the page can call into the parser and the
// terminal through ParserBridge and TerminalBridge.
var host = WebAssemblyHostBuilder.CreateDefault(args).Build();

// The bridge needs a way to call back into the page while a command runs, for live
// progress. The JS runtime is only reachable through the host's services.
TerminalBridge.Attach(host.Services.GetRequiredService<IJSRuntime>());

await host.RunAsync();
