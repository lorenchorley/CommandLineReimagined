# CommandLineReimagined

A command line where what you type is parsed into a tree rather than passed around as
text. Every word knows the role the grammar gave it, commands return values instead of
printing them, pipelines carry those values with their types intact, and every command
can be undone.

The terminal runs in a browser tab: the parser, the commands and the filesystem are
.NET compiled to WebAssembly, so nothing you type leaves the page. The same execution
layer drives a Windows desktop shell built on an entity component system.

```
$ ls | where $row.kind eq folder | select name
name
documents
examples
projects

$ cat documents/notes.txt | write backup.txt
backup.txt

$ undo
Undone: cat documents/notes.txt | write backup.txt

$ cd $row.kind eq folder
$row.kind eq folder

$ cat notes-from-yesterday.txt else echo "starting fresh"
starting fresh
```

A listing is a table, a predicate over it is a question, and a question is somewhere
you can be: after the `cd`, `ls` answers the query rather than a directory. A failure
is a value too: `else` recovers without leaving the line, and `try` keeps a fault for a
later stage to read. And a table can be kept: `to-xml` and `to-csv` write one to a
file, and `from-xml` and `from-csv` read it back as the same table.

## Try it

The browser terminal is published from this repository to
<https://lorenchorley.github.io/CommandLineReimagined/>. It is a static site: the
parser, the commands and the filesystem are .NET compiled to WebAssembly, so nothing
you type leaves the page, and your files are kept in the browser between visits.

## Documentation

- **[Documentation index](docs/README.md)** — start here.
- [Getting started](docs/getting-started.md) — open a terminal and run something.
- [Worked examples](docs/examples.md) — complete sessions to copy.
- [Tables and predicates](docs/tables.md) — filter, sort, count and group a listing; keep a table in XML or CSV.
- [The filesystem](docs/filesystem.md) — files as attribute records, queries as places.
- [The command language](docs/language.md) — every syntax the parser accepts.
- [Command reference](docs/commands.md) — one entry per command.
- [How it works](docs/concepts.md) — parse, bind, evaluate, commit, project.
- [Specification](docs/spec/README.md) — the normative definition.

## Building

Needs the .NET 10 SDK, plus the `wasm-tools` workload for the browser client. The
desktop shell needs Windows; everything else builds anywhere.

```bash
dotnet publish WebClient/WebClient.csproj -c Release -o publish
tools/postprocess-publish.sh publish/wwwroot
cd publish/wwwroot && python3 -m http.server 8080
```

See [Building and testing](docs/building.md) for the test suites, the continuous
integration jobs and how the browser payload is produced.

## Package diagram

[PlantUML Link](https://groupeisagri-tools-plantuml-appservice.azurewebsites.net/uml/TL6xRiCm3Dpr5KBs_4A1LkWG84EHD5q23uHC649aqg2K8IZoxv5ViHmxe-v1tedXugW1WLf_WRrKmbSg3NiM7GKe90efgl1KMQuRfeGTuysZ5hGHgFAer4_oGuLwOxYkWSgz8zpeVwNr-HUbTWI-Q1y37Gfrpqn_Bbp13_hJpenhGkSvmCr0Y8wgxhcZY08sEgOzLyxEHncMSV41gKGPROOf2dkFqXdEdqtZdOLsJA6tsuzdjZPwiSP_c-CgtgSlqin5j5wG5tYdcRT-fshRjOlog7cuZi-18Li6gUa6FJhTvvMPPr9vZtL1ziT3y-a4QbgqslY5NLvPQpNtB5QiqQVeV1h_0000)
