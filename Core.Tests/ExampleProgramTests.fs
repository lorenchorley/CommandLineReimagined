namespace CommandLineReimagined.Core.Tests

open System
open System.Text.RegularExpressions
open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>The example programs, against their golden results.</summary>
/// <remarks>
/// Unit tests prove parts; these prove the whole. The golden results in
/// docs/plan/examples.md are normative and were written before the code, so a
/// difference here is the implementation's to fix rather than the program's. Each
/// program is checked twice: line by line, so a failure names the line, and as a whole
/// file through `run`, which is how anyone else will meet it.
///
/// `*` in an expected result matches anything, which is what a timestamp is.
/// </remarks>
[<TestClass>]
type ExampleProgramTests() =

    /// The lines a guide shows failing on purpose, to show how a failure reads.
    let meantToFail = set [ "ls | where kind eq folder"; "ls | where $row.kind"; "read missing.txt" ]

    /// The example lines of a guide: the lines indented by two spaces, in order.
    let examplesOf (text: string) =
        text.Split('\n')
        |> Array.filter (fun line -> line.StartsWith "  " && line.Trim() <> "")
        |> Array.map (fun line -> line.Trim())
        |> List.ofArray

    /// The golden results for tables.clr, in the order the script runs them.
    let tablesProgram =
        [ "ls",
          "name        kind    folder  size  modified\n\
             documents   folder  /       0     *\n\
             examples    folder  /       0     *\n\
             guide       folder  /       0     *\n\
             projects    folder  /       0     *\n\
             readme.txt  text    /       193   *"

          "ls | where $row.kind eq folder | count", "4"

          "ls | sort name desc | first", "<row name=readme.txt kind=text folder=/ size=193 modified=*/>"

          "ls | select name kind | take 2",
          "name       kind\n\
             documents  folder\n\
             examples   folder"

          "set greeting hello", "hello"

          "set answer 42", "42"

          "vars",
          "name      value\n\
             answer    42\n\
             greeting  hello"

          "help | where $row.name eq set | select name description",
          "name  description\n\
             set   Bind a value, or whatever was piped in, to a variable" ]

    /// <summary>The golden results for journal.clr, in the order the script runs them.</summary>
    /// <remarks>
    /// Phase 4's program. It is the one that only works once a predicate is somewhere
    /// you can be: `in` on a question, `ls` listing it across folders, `out` putting it
    /// down again, and the event log giving the whole thing back with `undo`.
    /// </remarks>
    let journalProgram =
        [ "mkdir journal", "journal"
          "in journal", "journal"
          "save <note name=monday mood=good tag=work/>", "monday"
          "save <note name=tuesday mood=tired tag=work/>", "tuesday"
          "save <note name=saturday mood=great tag=home/>", "saturday"
          "echo \"Stand-up moved to ten.\" | write monday", "monday"
          "read monday", "Stand-up moved to ten."
          "attr tuesday mood=better", "tuesday"

          "ls",
          "name      kind  folder    size  modified  mood    tag\n\
             monday    note  /journal  22    *         good    work\n\
             saturday  note  /journal  0     *         great   home\n\
             tuesday   note  /journal  0     *         better  work"

          "find $row.kind eq note and $row.tag eq work | count", "2"

          "in $row.mood eq great", "$row.mood eq great"

          "ls",
          "name      kind  folder    size  modified  mood   tag\n\
             saturday  note  /journal  0     *         great  home"

          "out", "/journal"
          "rm tuesday", "Removed tuesday"
          "undo", "Undone: rm tuesday"
          "history | where $row.undone eq true | count", "1" ]

    /// <summary>The golden results for resilient.clr, in the order the script runs them.</summary>
    /// <remarks>
    /// Phase 5's program: failure as a value. `else` recovers without leaving the line,
    /// `try` holds a fault as something a later stage can bind and read, `??` defaults
    /// an answer of nothing, and a failed left branch leaves nothing behind.
    /// </remarks>
    let resilientProgram =
        [ "read notes-from-yesterday.txt else echo \"starting fresh\"", "starting fresh"
          "read notes-from-yesterday.txt else echo \"starting fresh\" | write today.txt", "today.txt"
          "read today.txt", "starting fresh"
          "try read nowhere.txt | set problem", "File does not exist : /nowhere.txt"
          "echo $problem.kind", "NotFound"
          "echo $problem.message", "File does not exist : /nowhere.txt"
          "first (ls | where $row.kind eq view) ?? \"no views yet\"", "no views yet"
          "first (ls | where $row.kind eq view) ?? \"no views yet\" | set latest", "no views yet"
          "echo $latest", "no views yet"
          "mkdir today | in nowhere else echo \"the whole line was rolled back\"", "the whole line was rolled back"

          "ls",
          "name        kind    folder  size  modified\n\
             documents   folder  /       0     *\n\
             examples    folder  /       0     *\n\
             guide       folder  /       0     *\n\
             projects    folder  /       0     *\n\
             readme.txt  text    /       193   *\n\
             today.txt   text    /       14    *" ]

    /// <summary>The golden results for inventory.clr, in the order the script runs them.</summary>
    /// <remarks>
    /// Phase 6's program: a tag becomes a file of XML, the file reads back as a table
    /// with its number columns numeric, and a question asked of it is written out as CSV
    /// and read back again. Decision 0026 added `sort qty` to the sixth line, which the
    /// golden result for `read reorder.csv` had always assumed.
    /// </remarks>
    let inventoryProgram =
        [ "mkdir stock", "stock"
          "in stock", "stock"

          "<items><item sku=A1 name=bolts qty=120 min=50/><item sku=B2 name=nuts qty=12 min=40/><item sku=C3 name=washers qty=0 min=20/></items> | to-xml items.xml",
          "items.xml"

          "from-xml items.xml | count", "3"

          "from-xml items.xml | where $row.qty lt $row.min | sort qty | select sku name qty min",
          "sku  name     qty  min\n\
             C3   washers  0    20\n\
             B2   nuts     12   40"

          "from-xml items.xml | where $row.qty lt $row.min | sort qty | select sku qty | to-csv reorder.csv",
          "reorder.csv"

          "from-csv reorder.csv | count", "2"

          "read reorder.csv",
          "sku,qty\n\
             C3,0\n\
             B2,12"

          "from-xml items.xml | sort qty desc | first", "<row sku=A1 name=bolts qty=120 min=50/>" ]

    /// <summary>Whether a result matches a golden one, with `*` for anything.</summary>
    /// <remarks>
    /// Matched line by line so that a timestamp in a column does not let a `*` swallow
    /// the line breaks around it, which would make a one-line answer match a table.
    ///
    /// A run of spaces in a golden result matches a run of spaces, of any length. A
    /// table's columns are padded to the widest cell in them, and the widest cell in
    /// the `modified` column is a thirty-three character timestamp that the golden
    /// results write as `*` — so pinning the padding would be pinning the length of
    /// something the golden deliberately does not say. What the goldens are about is
    /// the cells and the order they come in, and that is what this holds them to.
    /// </remarks>
    ///
    /// One line break at the very end of an answer is not a line of it. A golden result
    /// is written as the lines a person sees, and a file's text — which is what `read`
    /// answers — ends in one, as every line of a CSV does; the exact text is pinned
    /// separately, where it is the point.
    let matches (expected: string) (actual: string) =
        let lines (text: string) = text.Replace("\r\n", "\n").Split '\n'
        let expectedLines = lines expected
        let actualLines = lines (if actual.EndsWith "\n" then actual.Substring(0, actual.Length - 1) else actual)

        let matchesLine (pattern: string) (text: string) =
            let escaped =
                pattern.Split '*'
                // Escaping turns each space into `\ `, so the run to loosen is written
                // in the escaped alphabet rather than the original one.
                |> Array.map (fun part -> Regex.Replace(Regex.Escape part, @"(\\ )+", "[ ]+"))
                |> String.concat ".*"

            Regex.IsMatch(text, "^" + escaped + "$")

        expectedLines.Length = actualLines.Length
        && Array.forall2 matchesLine expectedLines actualLines

    let assertMatches (source: string) (expected: string) (actual: string) =
        if not (matches expected actual) then
            Assert.Fail(
                sprintf "'%s' answered\n%s\n\nbut the golden result is\n%s" source actual expected)

    // ------------------------------------------------------------- tables.clr

    /// Line by line, so a failure names the line that produced it.
    [<TestMethod>]
    member _.TablesLineByLine() =
        let harness = seeded ()

        for source, expected in tablesProgram do
            assertMatches source expected (harness.Text source)

    /// <summary>The whole file, through `run`, from a fresh session.</summary>
    /// <remarks>
    /// The same lines reach the same place by the other road: each commits its own
    /// transaction (decision 0020), so the filesystem, the variables and the location
    /// end up identical to running them by hand.
    /// </remarks>
    [<TestMethod>]
    member _.TablesThroughRun() =
        let byHand = seeded ()

        for source, _ in tablesProgram do
            byHand.Run source |> ignore

        let byScript = seeded ()
        byScript.Run "run examples/tables.clr" |> ignore

        Assert.AreEqual<Map<FileId, FileRecord>>(byHand.Projection.Files, byScript.Projection.Files)
        Assert.AreEqual<Map<string, Value>>(byHand.Projection.Variables, byScript.Projection.Variables)
        Assert.AreEqual<Location>(byHand.Projection.Location, byScript.Projection.Location)

    /// The run is legible: each line is echoed before its result, so the scrollback
    /// shows what happened rather than only where it ended up.
    [<TestMethod>]
    member _.RunEchoesEveryLineItRuns() =
        let harness = seeded ()
        let written = harness.Written "run examples/tables.clr"

        for source, _ in tablesProgram do
            Assert.IsTrue(
                written |> List.contains ("> " + source),
                sprintf "The run did not echo '%s'." source)

    /// The value of the last line, which is what a script answers.
    [<TestMethod>]
    member _.RunAnswersItsLastLine() =
        let harness = seeded ()

        let expected = tablesProgram |> List.last |> snd

        assertMatches "run examples/tables.clr" expected (harness.Text "run examples/tables.clr")

    // ------------------------------------------------------------ journal.clr

    [<TestMethod>]
    member _.JournalLineByLine() =
        let harness = seeded ()

        for source, expected in journalProgram do
            assertMatches source expected (harness.Text source)

    [<TestMethod>]
    member _.JournalThroughRun() =
        let byHand = seeded ()

        for source, _ in journalProgram do
            byHand.Run source |> ignore

        let byScript = seeded ()
        byScript.Run "run examples/journal.clr" |> ignore

        Assert.AreEqual<Map<FileId, FileRecord>>(byHand.Projection.Files, byScript.Projection.Files)
        Assert.AreEqual<Map<string, Value>>(byHand.Projection.Variables, byScript.Projection.Variables)
        Assert.AreEqual<Location>(byHand.Projection.Location, byScript.Projection.Location)

    /// <summary>What the program is for, stated as an assertion.</summary>
    /// <remarks>
    /// The plan's sentence after the golden results: "After the run, `tuesday` exists
    /// again with `mood = better`: undo restored the record as it was, attributes
    /// included." An undo that put the file back but lost what was written on it would
    /// pass every line above and still be the wrong undo.
    /// </remarks>
    [<TestMethod>]
    member _.UndoRestoresTheRecordWithItsAttributes() =
        let harness = seeded ()
        harness.Run "run examples/journal.clr" |> ignore

        Assert.IsTrue(harness.Exists "/journal/tuesday", "The undone rm should have brought tuesday back.")
        Assert.AreEqual<string>("better", harness.Attribute "/journal/tuesday" "mood")
        Assert.AreEqual<string>("work", harness.Attribute "/journal/tuesday" "tag")

    // ---------------------------------------------------------- resilient.clr

    [<TestMethod>]
    member _.ResilientLineByLine() =
        let harness = seeded ()

        for source, expected in resilientProgram do
            assertMatches source expected (harness.Text source)

    [<TestMethod>]
    member _.ResilientThroughRun() =
        let byHand = seeded ()

        for source, _ in resilientProgram do
            byHand.Run source |> ignore

        let byScript = seeded ()
        byScript.Run "run examples/resilient.clr" |> ignore

        Assert.AreEqual<Map<FileId, FileRecord>>(byHand.Projection.Files, byScript.Projection.Files)
        Assert.AreEqual<Map<string, Value>>(byHand.Projection.Variables, byScript.Projection.Variables)
        Assert.AreEqual<Location>(byHand.Projection.Location, byScript.Projection.Location)

    /// <summary>`try` answered a fault value, not an error: the line succeeded.</summary>
    /// <remarks>
    /// The golden result for `try read nowhere.txt | set problem` reads like an error
    /// message and is not one, which is the whole of the point; this is the half of it
    /// that the display string cannot show.
    /// </remarks>
    [<TestMethod>]
    member _.TryAnswersAFaultValueNotAnError() =
        let harness = seeded ()
        let response = harness.Respond "try read nowhere.txt | set problem"

        Assert.AreEqual<Fault option>(None, response.Fault)

        match response.Result with
        | Some(Value.Fault fault) -> Assert.AreEqual<FaultKind>(NotFound, fault.Kind)
        | other -> Assert.Fail(sprintf "Expected a fault value, got %A." other)

    /// <summary>What the program is for, stated as an assertion.</summary>
    /// <remarks>
    /// The plan's sentence after the golden results: "No folder named `today` exists:
    /// the left side of the `else` failed at `in`, so the `mkdir` before it was never
    /// committed."
    /// </remarks>
    [<TestMethod>]
    member _.TheRolledBackFolderDoesNotExist() =
        let harness = seeded ()
        harness.Run "run examples/resilient.clr" |> ignore

        Assert.IsFalse(harness.Exists "/today", "The mkdir on the failed side of else was committed.")
        Assert.IsTrue(harness.Exists "/today.txt")

    // ---------------------------------------------------------- inventory.clr

    [<TestMethod>]
    member _.InventoryLineByLine() =
        let harness = seeded ()

        for source, expected in inventoryProgram do
            assertMatches source expected (harness.Text source)

    [<TestMethod>]
    member _.InventoryThroughRun() =
        let byHand = seeded ()

        for source, _ in inventoryProgram do
            byHand.Run source |> ignore

        let byScript = seeded ()
        byScript.Run "run examples/inventory.clr" |> ignore

        Assert.AreEqual<Map<FileId, FileRecord>>(byHand.Projection.Files, byScript.Projection.Files)
        Assert.AreEqual<Map<string, Value>>(byHand.Projection.Variables, byScript.Projection.Variables)
        Assert.AreEqual<Location>(byHand.Projection.Location, byScript.Projection.Location)

    /// <summary>The CSV text exactly, terminal line break included.</summary>
    /// <remarks>
    /// The plan asks for "the exact CSV text", and the line-by-line golden cannot show
    /// the line break that ends the last record; this can.
    /// </remarks>
    [<TestMethod>]
    member _.TheReorderFileIsExactlyTheGoldenCsv() =
        let harness = seeded ()
        harness.Run "run examples/inventory.clr" |> ignore

        Assert.AreEqual<string>("sku,qty\nC3,0\nB2,12\n", harness.Content "/stock/reorder.csv")
        Assert.AreEqual<string>("csv", harness.Attribute "/stock/reorder.csv" "kind")
        Assert.AreEqual<string>("xml", harness.Attribute "/stock/items.xml" "kind")

    /// <summary>What the program is for, stated as an assertion.</summary>
    /// <remarks>
    /// The plan's sentence after the golden results: `qty` and `min` are number columns
    /// because every value parses as a number, which is what makes `lt` and `sort qty`
    /// numeric. Textually, "120" sorts before "12" and "0" is less than nothing useful.
    /// </remarks>
    [<TestMethod>]
    member _.TheDocumentsNumberColumnsAreNumbers() =
        let harness = seeded ()
        harness.Run "run examples/inventory.clr" |> ignore

        Assert.AreEqual<string>(
            "name  type\nsku   text\nname  text\nqty   number\nmin   number",
            harness.Text "from-xml items.xml | columns")

        Assert.AreEqual<string>("name  type\nsku   text\nqty   number", harness.Text "from-csv reorder.csv | columns")

    // ------------------------------------------------------------------- run

    [<TestMethod>]
    member _.EveryExampleIsSeeded() =
        let harness = seeded ()

        Assert.AreEqual<string>(
            "inventory.clr journal.clr resilient.clr tables.clr",
            harness.Names "ls examples")

    [<TestMethod>]
    member _.AScriptIsAScript() =
        let harness = seeded ()

        Assert.AreEqual<string>("script", harness.Attribute "examples/tables.clr" "kind")

    /// Each line commits its own transaction, so undo after a script steps back a line
    /// at a time rather than taking the whole file away.
    [<TestMethod>]
    member _.EachLineCommitsItsOwnTransaction() =
        let harness = seeded ()
        harness.Run "run examples/tables.clr" |> ignore

        Assert.AreEqual<string list>([ "seed"; "set greeting hello"; "set answer 42" ], harness.History())
        Assert.AreEqual<string>("Undone: set answer 42", harness.Text "undo")

    [<TestMethod>]
    member _.AFaultNamesTheScriptAndTheLine() =
        let harness = seeded ()
        harness.Run "write broken.clr \"echo one\nin nowhere\necho three\"" |> ignore

        let fault = harness.Fail "run broken.clr"

        Assert.AreEqual<string>("/broken.clr line 2: Directory does not exist : nowhere", fault.Message)
        Assert.AreEqual<FaultKind>(NotFound, fault.Kind)

    /// Blank and commented lines are skipped and still counted, so the number in a
    /// message is the one an editor shows.
    [<TestMethod>]
    member _.SkippedLinesAreStillCounted() =
        let harness = seeded ()
        harness.Run "write counted.clr \"# a comment\n\necho one\nin nowhere\"" |> ignore

        StringAssert.Contains(harness.Error "run counted.clr", "line 4:")

    [<TestMethod>]
    member _.RunningANonScriptFileStillRunsItsLines() =
        let harness = seeded ()
        harness.Run "write plain.txt \"echo hello\"" |> ignore

        Assert.AreEqual<string>("hello", harness.Text "run plain.txt")

    [<TestMethod>]
    member _.AScriptThatRunsItselfStopsAtTheDepthLimit() =
        let harness = seeded ()
        harness.Run "write loop.clr \"run loop.clr\"" |> ignore

        StringAssert.Contains(harness.Error "run loop.clr", "8 deep")

    // ------------------------------------------------------------------ the guide

    /// <summary>The readme points into the guide, and the guide is seeded whole.</summary>
    /// <remarks>
    /// The page's banner says only to read `readme.txt`, so a readme pointing nowhere
    /// would leave a new visitor with nothing. Every guide names the next one, and the
    /// last one names none.
    /// </remarks>
    [<TestMethod>]
    member _.TheReadmeLeadsThroughTheWholeGuide() =
        let harness = seeded ()
        let guides = harness.Column "ls guide" "name"

        Assert.AreEqual<int>(Seed.guideFiles.Length, guides.Length)
        StringAssert.Contains(harness.Text "read readme.txt", "read guide/1-start.txt")

        for guide in guides |> List.take (guides.Length - 1) do
            let next = Regex.Match(harness.Text("read guide/" + guide), @"Next: read guide/(\S+)")
            Assert.IsTrue(next.Success, sprintf "%s names no next guide." guide)
            Assert.IsTrue(harness.Exists("/guide/" + next.Groups[1].Value), sprintf "%s names %s, which is not there." guide next.Groups[1].Value)

    /// <summary>Every example in the guide runs, from a fresh tab, in the order shown.</summary>
    /// <remarks>
    /// The guide is what a visitor reads first, so it is held to what the terminal does:
    /// a line that stops working, or starts working when it is shown failing, fails here.
    /// </remarks>
    [<TestMethod>]
    member _.EveryExampleInTheGuideRuns() =
        for guide in Seed.guideFiles do
            let harness = seeded ()

            for line in examplesOf (defaultArg guide.Content "") do
                let response = harness.Respond line

                match response.Fault, meantToFail.Contains line with
                | Some fault, false -> Assert.Fail(sprintf "%s: '%s' failed: %s" guide.Name line fault.Message)
                | None, true -> Assert.Fail(sprintf "%s: '%s' is shown failing, and did not." guide.Name line)
                | _ -> ()
