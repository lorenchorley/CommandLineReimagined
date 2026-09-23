namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// Command names at the head of a line and after a pipe (stream A).
[<TestClass>]
type CommandCompletionTests() =

    let texts (harness: Harness) (line: string) =
        Async.RunSynchronously(harness.Session.Complete(line, line.Length)).Items |> List.map (fun c -> c.Text)

    /// The smoke test the foundation leaves: the stream fills in the rest.
    [<TestMethod>]
    member _.ACommandNameCompletes() =
        let harness = seeded ()

        assertContains "where" (texts harness "wh")
