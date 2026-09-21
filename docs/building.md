# Building and testing

What you need, how to run the suites, and how the browser build is produced.

## Prerequisites

| Target | Needs |
| --- | --- |
| Libraries, tests, web | .NET SDK 8 |
| WebAssembly client | .NET SDK 8 plus the `wasm-tools` workload |
| Desktop shell | Windows, and .NET 7 or a roll-forward to 8 |

Most projects target .NET 7. Building them with only the .NET 8 SDK installed works,
but running the tests needs a roll-forward:

```bash
export DOTNET_ROLL_FORWARD=Major
```

Without it, `dotnet test` aborts with a message about installing .NET 7.

## Build

Everything except the desktop shell builds on any platform:

```bash
dotnet build CommandLineReimagined.sln -c Release          # Windows only
dotnet build Web.Core/Web.Core.csproj -c Release           # anywhere
dotnet build Commands/Commands.csproj -c Release           # anywhere
```

`CommandLineReimagined/Application.csproj` is the WPF shell and only builds on Windows.
`WebClient/WebClient.csproj` needs the workload:

```bash
dotnet workload install wasm-tools
```

## Test

| Project | Covers |
| --- | --- |
| `Parser.Tests` | The grammar, both parsers, error positions and serialisation. |
| `Execution.Tests` | Binding, pipes, tags, variables, every command and undo. |
| `Web.Core.Tests` | The terminal session: streaming, cancellation, completion, undo. |
| `Terminal.Tests` | Naming and path helpers. |
| `Utils.Tests`, `EntityComponentSystem.Tests`, `SourceGenerators.Tests`, `Rendering.Tests` | The supporting libraries. |

```bash
export DOTNET_ROLL_FORWARD=Major
for p in $(find . -name '*.Tests.csproj' | sort); do dotnet test "$p" -c Release; done
```

## Run the web client locally

```bash
dotnet publish WebClient/WebClient.csproj -c Release -o publish
cd publish/wwwroot && python3 -m http.server 8080
```

Open <http://127.0.0.1:8080>. Any static server works, as long as it serves `.wasm`
with the `application/wasm` content type.

To run the ASP.NET host instead, which adds `/healthz`, `/api/parse` and a WebSocket
at `/ws`:

```bash
dotnet run --project Web
```

## Publish to a static host that reserves underscored paths

Some hosts refuse paths beginning with an underscore, which is where Blazor puts its
runtime. Rename the folder and rewrite the references:

```bash
cd publish/wwwroot
find . \( -name '*.gz' -o -name '*.br' \) -delete
mv _framework framework
grep -rl _framework --include=*.js --include=*.json --include=*.html . \
  | xargs sed -i 's/_framework/framework/g'
```

The client sets `BlazorCacheBootResources=false` so renaming does not break integrity
checks. The payload after this is about 14 MB across roughly 100 files.

## Continuous integration

`.github/workflows/build.yml` runs three jobs on every push:

| Job | Runner | What it does |
| --- | --- | --- |
| Full solution | Windows | Builds the whole solution, including the desktop shell, and runs every test project. |
| Libraries and tests | Linux | Builds every project except the desktop shell and the WebAssembly client, and runs every test project. |
| WebAssembly client | Linux | Installs `wasm-tools`, publishes the client and fails if the payload exceeds 20 MB. |

`deploy.yml` deploys to Azure App Service and skips itself when no `AZURE_CREDENTIALS`
secret is configured.

## Keeping the payload small

The search data for command suggestions is 20 MB of dictionary and thesaurus files.
They are content files copied next to the assembly rather than embedded resources, so
the WebAssembly client simply does not ship them and `Terminal.dll` stays small. Code
that uses them treats their absence as "synonym search unavailable" rather than as an
error.
