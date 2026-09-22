# Building and testing

What you need, how to run the suites, and how the browser build is produced.

## Prerequisites

| Target | Needs |
| --- | --- |
| Libraries, tests, web | .NET SDK 10 |
| WebAssembly client | .NET SDK 10 plus the `wasm-tools` workload |
| Desktop shell | Windows, and .NET SDK 10 |

Every project targets .NET 10, except the source generator, which targets
netstandard2.0 because that is what a Roslyn component must target. `global.json`
asks for SDK 10.0.100 or a newer feature band, so a machine with an older SDK is told
so rather than failing later with a confusing error.

Package versions live in one place, `Directory.Packages.props`. Project files
reference packages by name and carry no version, so a dependency is upgraded once
rather than once per project.

## Build

Everything except the desktop shell builds on any platform:

```bash
dotnet build CommandLineReimagined.sln -c Release          # Windows only
dotnet build Core/Core.fsproj -c Release                   # anywhere
dotnet build Web.Core/Web.Core.csproj -c Release           # anywhere
```

Two projects are F#: `Parser.FParsec` (the grammar) and `Core` (everything the
language means). A loop over projects has to glob `*.fsproj` as well as `*.csproj`, or
it silently builds everything that depends on the core without building the core's own
tests.

`CommandLineReimagined/Application.csproj` is the WPF shell and only builds on Windows.
`WebClient/WebClient.csproj` needs the workload:

```bash
dotnet workload install wasm-tools
```

## Test

| Project | Covers |
| --- | --- |
| `Parser.Tests` | The grammar, both parsers, error positions and serialisation. |
| `Core.Tests` | Values, faults, the store, undo, binding, pipes, tags, every command, completion. |
| `Web.Core.Tests` | The adapter: DTO shapes, streaming, cancellation, completion. |
| `Terminal.Tests` | Naming and path helpers. |
| `Utils.Tests`, `EntityComponentSystem.Tests`, `SourceGenerators.Tests`, `Rendering.Tests` | The supporting libraries. |

```bash
for p in $(find . \( -name '*.Tests.csproj' -o -name '*.Tests.fsproj' \) | sort); do
  dotnet test "$p" -c Release
done
```

`Core.Tests` is F#, and it absorbed `Execution.Tests`. The cases are the same cases;
what changed is what they assert against. The old ones created a temporary directory
and checked the disk afterwards, so they could only say that something had happened
somewhere. These read the projection, so they say exactly what a line changed, and
they run without a filesystem at all.

## Run the web client locally

```bash
dotnet publish WebClient/WebClient.csproj -c Release -o publish
cd publish/wwwroot
mv _framework framework
grep -rl _framework --include=*.js --include=*.json --include=*.html . \
  | xargs sed -i 's/_framework/framework/g'
python3 -m http.server 8080
```

Open <http://127.0.0.1:8080>. Any static server works, as long as it serves `.wasm`
with the `application/wasm` content type.

The rename is not optional, which this document used to imply it was by listing it
only under deploying. The page asks for `framework/blazor.webassembly.js`, because the
host it is deployed to refuses paths beginning with an underscore, so a folder straight
out of `publish` 404s on its own runtime and shows a page that never starts.
`tools/browser-check.mjs` does the rename itself, so running the check needs only the
publish.

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
checks, and turns off .NET 10's asset fingerprinting (`WasmFingerprintAssets`) so that
the runtime files keep stable names. With fingerprinting on, every publish writes a
fresh set of hashed names and a host that keeps what it is not told to replace ends up
storing the runtime several times over.

The payload after this is about 15 MB across 121 files.

## Check the page in a browser

The test suites prove the core. This proves the page: that the runtime boots, that the
bridge is wired to it, and that what a person sees at phone size is what the
acceptance list says. Those are the failures a unit test cannot have — a stale interop
name, a renderer that drops a field, a console error nobody reads, a chip too small to
tap.

```bash
dotnet publish WebClient/WebClient.csproj -c Release -o publish
node tools/browser-check.mjs publish/wwwroot
```

It needs Node and Playwright's Chromium:

```bash
npm install --no-save playwright
npx playwright install --with-deps chromium
```

Set `PLAYWRIGHT_CHROMIUM` to an executable to use one that is already installed, which
is what a sandbox with a preloaded browser needs:

```bash
PLAYWRIGHT_CHROMIUM=/opt/pw-browsers/chromium node tools/browser-check.mjs publish/wwwroot
```

It runs the page at 390 by 844 at a device scale factor of 3 — a phone, which is what
the page is designed for and therefore the only size worth checking — submits the
acceptance lines for the current phase in one session, and fails on any mismatch or any
console error.

## Continuous integration

`.github/workflows/build.yml` runs three jobs on every push:

| Job | Runner | What it does |
| --- | --- | --- |
| Full solution | Windows | Builds the whole solution, including the desktop shell, and runs every test project. |
| Libraries and tests | Linux | Builds every project except the desktop shell and the WebAssembly client, and runs every test project. |
| WebAssembly client | Linux | Installs `wasm-tools`, publishes the client, fails if the payload exceeds 20 MB, and runs the browser check. |

`deploy.yml` deploys to Azure App Service and skips itself when no `AZURE_CREDENTIALS`
secret is configured.

## Keeping the payload small

The search data for command suggestions is 20 MB of dictionary and thesaurus files.
They are content files copied next to the assembly rather than embedded resources, so
the WebAssembly client simply does not ship them and `Terminal.dll` stays small. Code
that uses them treats their absence as "synonym search unavailable" rather than as an
error.
