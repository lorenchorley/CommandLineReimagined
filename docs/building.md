# Building and testing

What you need, how to run the suites, and how the browser build is produced.

## Prerequisites

| Target | Needs |
| --- | --- |
| Libraries, tests, web | .NET SDK 10 |
| WebAssembly client | .NET SDK 10 plus the `wasm-tools` workload |

Every project targets .NET 10, and every one builds on any platform. `global.json`
asks for SDK 10.0.100 or a newer feature band, so a machine with an older SDK is told
so rather than failing later with a confusing error.

Package versions live in one place, `Directory.Packages.props`. Project files
reference packages by name and carry no version, so a dependency is upgraded once
rather than once per project.

## Build

Build the whole solution, or one project at a time:

```bash
dotnet build CommandLineReimagined.sln -c Release
dotnet build Core/Core.fsproj -c Release
dotnet build Web.Core/Web.Core.csproj -c Release
```

Two projects are F#: `Parser.FParsec` (the grammar) and `Core` (everything the
language means). A loop over projects has to glob `*.fsproj` as well as `*.csproj`, or
it silently builds everything that depends on the core without building the core's own
tests.

Every project builds without a warning, and CI's logs are the place a new one shows up
first. A nullable warning is fixed with an annotation, not suppressed.

`WebClient/WebClient.csproj` needs the workload:

```bash
dotnet workload install wasm-tools
```

## Test

| Project | Covers |
| --- | --- |
| `Parser.Tests` | The grammar, error positions and serialisation, including every input the removed GOLD parser was once compared on ([0055](decisions/0055-the-gold-parser-is-removed.md)). |
| `Core.Tests` | Values, faults, the store, undo, binding, pipes, tags, tables, predicates, views, recovery, XML and CSV, persistence, every command, completion, the four example programs against their golden results, and every example line in the seeded guide. |
| `Web.Core.Tests` | The adapter and the stored log: DTO shapes, streaming, cancellation, completion, and the versioned JSON a transaction is kept in. |

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
tools/postprocess-publish.sh publish/wwwroot
cd publish/wwwroot
python3 -m http.server 8080
```

Open <http://127.0.0.1:8080>. Any static server works, as long as it serves `.wasm`
with the `application/wasm` content type.

The rename is not optional, which this document used to imply it was by listing it
only under deploying. The page asks for `framework/blazor.webassembly.js`, because the
host it is deployed to refuses paths beginning with an underscore, so a folder straight
out of `publish` 404s on its own runtime and shows a page that never starts.
`tools/postprocess-publish.sh` does the rename, deletes the precompressed copies and
sets `<base href>`; `tools/browser-check.mjs` does the rename itself, so running the
check needs only the publish.

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

The payload after this is 10.1 MB across 72 files, which is how the CI guard measures it.

## Re-run the documentation's transcripts

Every transcript in `docs/` is pasted from real output, never typed. To produce one,
write the lines as `$ <command>`, one per line, with `---` wherever a fresh tab starts,
and run them against the core:

```bash
dotnet build Web.Core.Tests/Web.Core.Tests.csproj -c Release
dotnet fsi tools/transcript.fsx lines.txt
```

The clock is pinned to the time the documentation shows and ids count up, so the same
input prints the same output every time, and a line that does not parse is worded the
way the page words it. What only the page can show, such as a tap, a live listing or a
reload, is checked by the browser check instead.

## Check the log's storage

`WebClient/wwwroot/store.js` keeps the log in the browser's IndexedDB. Most of its job
is failure behaviour — a private window, storage that goes away mid-session — which is
awkward to arrange in a real browser and trivial outside one:

```bash
npm install --no-save fake-indexeddb
node tools/store-check.mjs
```

It runs in a second and is part of the Linux CI job.

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
the page is designed for and therefore the only size worth checking — submits every
phase's acceptance lines in one session, runs the four example programs with
`run examples/<name>.clr` against their golden results, reloads the page to prove the log
came back, and fails on any mismatch or any console error.

The two checks need Node and are declared in `tools/package.json`, so
`npm install --prefix tools` installs exactly the versions CI uses rather than
whatever is newest that day.

## Hosting it on GitHub Pages

The client is a static site, so a plain file host is all it needs and there is no
secret to configure. `.github/workflows/pages.yml` publishes it on every push to
`main` or a `claude/**` branch:

| Job | What it does |
| --- | --- |
| Build the client | Publishes, runs the post-processing script with the path Pages serves the site under, and refuses a payload over 20 MB. |
| Deploy | Uploads the folder and deploys it. |
| Check the live site | Installs Chromium and runs `tools/browser-check.mjs` against the deployed URL. A deployment that builds and uploads but does not run is not a deployment. |

The site is at <https://lorenchorley.github.io/CommandLineReimagined/>.

Two things a project site needs, both handled by
[`tools/postprocess-publish.sh`](../tools/postprocess-publish.sh):

- **`<base href>` must match the path.** A project site is served under
  `/CommandLineReimagined/`, not the root, and every asset the loader asks for is
  relative to that. The workflow passes the path that `actions/configure-pages`
  reports, so a user site or a custom domain needs no change here.
- **`.nojekyll`.** Without it, Pages runs the output through Jekyll, which ignores
  every file and folder beginning with an underscore. The framework folder is renamed
  anyway, for hosts that reserve those paths, but the marker costs nothing and removes
  a whole class of confusing 404.

Whichever branch pushes last is what the site shows, so while the work is on a branch
the live site follows that branch. Narrow the trigger to `main` once it merges.

Pages is enabled by the first run. If that is refused, the repository owner sets
Settings, then Pages, then Source to "GitHub Actions", once.

## Republishing the Artifact

The client is also published as a private claude.ai Artifact, at
<https://claude.ai/artifact/CcDmgfyanWjtEC2b6cs8mU>. Only its owner, and whoever they
share it with, can open it. Republishing is done from a Claude Code session, because
it needs the Artifact tool:

```bash
tools/prepare-artifact.sh /tmp/artifact
```

That publishes the client, post-processes it for the root path, and writes
`artifact.html`, the page, and `files.json`, the map of the 71 files served beside it.
Then publish with the Artifact tool:

| Field | Value |
| --- | --- |
| `url` | the Artifact's address above, so the link stays the same |
| `file_path` | `/tmp/artifact/artifact.html` |
| `root` | `/tmp/artifact` |
| `files` | the contents of `/tmp/artifact/files.json` |
| `label` | a few words on what changed |

When only `index.html` changed, publish the page alone, without `files`: the files
already published are kept.

The page is the title, the icon, the styles and the body, not a whole document,
because the service wraps the page in a document of its own. It has no `<base href>`,
so it resolves `framework/` beside itself. To check it in that form before publishing,
serve a copy with the page wrapped in a bare document under a sub-path, then run
`node tools/browser-check.mjs --url http://127.0.0.1:<port>/<sub-path>/index.html`.

## Continuous integration

`.github/workflows/build.yml` runs three jobs on every push:

| Job | Runner | What it does |
| --- | --- | --- |
| Libraries and tests | Linux | Builds every project except the WebAssembly client, runs every test project, and checks the store module. |
| WebAssembly client | Linux | Installs `wasm-tools`, publishes the client, fails if the payload exceeds 20 MB, and hands the published site to the next job. |
| Browser check | Linux | Installs Chromium and runs `tools/browser-check.mjs` against the site the previous job published and measured. |

`deploy.yml` deploys to Azure App Service and skips itself when no `AZURE_CREDENTIALS`
secret is configured.
