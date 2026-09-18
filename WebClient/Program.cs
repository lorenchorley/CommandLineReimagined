using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// No Blazor component tree: the UI is plain HTML and canvas in wwwroot/index.html.
// This host exists only to boot the .NET runtime so the browser can call into the
// parser through ParserBridge.
await WebAssemblyHostBuilder.CreateDefault(args).Build().RunAsync();
