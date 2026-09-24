// Runs documentation transcripts against the real core, the way the page would.
//
//   dotnet build Web.Core.Tests/Web.Core.Tests.csproj -c Release
//   dotnet fsi tools/transcript.fsx <input-file>
//
// The input holds one command per line written as `$ <command>`; a line `---` starts a
// fresh session, which is a fresh tab. Anything else is ignored, so a Markdown file's
// transcript can be copied in as it stands. The output is each command, what it wrote
// while it ran, and its result or the fault's message with the kind beside it in
// brackets (the brackets are this script's, not the screen's). Then each note the
// terminal adds (decision 0041), after its kind (`  suggestion:`, `  explanation:`), and
// each of its fixes as `  fix:`, which the page draws as a guidance panel and a chip.
//
// The clock is pinned to the time every transcript in docs/ shows, and ids count up,
// so two runs of the same input print the same thing. Syntax errors are worded by the
// web adapter, which is where the page's wording comes from.

#r "../Web.Core.Tests/bin/Release/net10.0/FParsec.dll"
#r "../Web.Core.Tests/bin/Release/net10.0/FParsecCS.dll"
#r "../Web.Core.Tests/bin/Release/net10.0/OneOf.dll"
#r "../Web.Core.Tests/bin/Release/net10.0/Parser.Tree.dll"
#r "../Web.Core.Tests/bin/Release/net10.0/Parser.FParsec.dll"
#r "../Web.Core.Tests/bin/Release/net10.0/CommandLineReimagined.Core.dll"
#r "../Web.Core.Tests/bin/Release/net10.0/Web.Core.dll"

open System
open CommandLineReimagined.Core

let fresh () =
    let mutable counter = 0
    let newId () = counter <- counter + 1; sprintf "id-%d" counter
    let clock () = DateTimeOffset(2026, 9, 22, 9, 30, 0, TimeSpan.Zero)
    let options = { SessionOptions.defaults with Clock = clock; NewId = newId }
    let session = Session(InMemoryLog(), options, SessionOptions.standardSeed options)
    Async.RunSynchronously(session.Initialize())
    session

// Only for the wording of a line that does not parse, which does not depend on state.
let web = CommandLineReimagined.Web.TerminalSession()
web.InitializeAsync().Wait()

let parser = CommandLineReimagined.Web.Parsing.CommandParseService()

let mutable session = fresh ()

for raw in IO.File.ReadAllLines fsi.CommandLineArgs.[1] do
    if raw.Trim() = "---" then
        session <- fresh ()
        printfn "---"
    elif raw.StartsWith "$ " then
        let line = raw.Substring 2
        let response = Async.RunSynchronously(session.Execute line)
        printfn "$ %s" line
        for written in response.Output do printfn "%s" written

        match response.Fault with
        // A script line that does not parse is the script's fault, worded by the core;
        // only a typed line that does not parse is worded by the adapter.
        | Some fault when fault.Kind = FaultKind.Syntax && not (isNull (parser.Parse(line).Error)) ->
            printfn "%s" (web.ExecuteAsync(line).Result.Error)
            printfn "  [Syntax]"
        | Some fault ->
            printfn "%s" fault.Message
            printfn "  [%A]" fault.Kind
        | None ->
            match response.Result with
            | Some value when value <> Value.Empty -> printfn "%s" (Value.display value)
            | _ -> ()

        for note in response.Notes do
            printfn "  %s: %s" note.Kind note.Text
            for fix in Note.fixLines note do printfn "  fix: %s" fix

        printfn ""
