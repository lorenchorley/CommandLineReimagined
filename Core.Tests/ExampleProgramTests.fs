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

    /// The golden results for tables.clr, in the order the script runs them.
    let tablesProgram =
        [ "ls",
          "name        kind    folder  size  modified\n\
             documents   folder  /       0     *\n\
             examples    folder  /       0     *\n\
             projects    folder  /       0     *\n\
             readme.txt  text    /       41    *"

          "ls | where $row.kind eq folder | count", "3"

          "ls | sort name desc | first", "<row name=readme.txt kind=text folder=/ size=41 modified=*/>"

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

    /// <summary>Whether a result matches a golden one, with `*` for anything.</summary>
    /// <remarks>
    /// Matched line by line so that a timestamp in a column does not let a `*` swallow
    /// the line breaks around it, which would make a one-line answer match a table.
    /// </remarks>
    let matches (expected: string) (actual: string) =
        let lines (text: string) = text.Replace("\r\n", "\n").Split '\n'
        let expectedLines = lines expected
        let actualLines = lines actual

        let matchesLine (pattern: string) (text: string) =
            let escaped =
                pattern.Split '*' |> Array.map Regex.Escape |> String.concat ".*"

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
        harness.Run "write broken.clr \"echo one\ncd nowhere\necho three\"" |> ignore

        let fault = harness.Fail "run broken.clr"

        Assert.AreEqual<string>("/broken.clr line 2: Directory does not exist : nowhere", fault.Message)
        Assert.AreEqual<FaultKind>(NotFound, fault.Kind)

    /// Blank and commented lines are skipped and still counted, so the number in a
    /// message is the one an editor shows.
    [<TestMethod>]
    member _.SkippedLinesAreStillCounted() =
        let harness = seeded ()
        harness.Run "write counted.clr \"# a comment\n\necho one\ncd nowhere\"" |> ignore

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
