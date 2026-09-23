namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// Variables, their members and tags (stream C).
[<TestClass>]
type VariableCompletionTests() =

    let texts (harness: Harness) (line: string) =
        Async.RunSynchronously(harness.Session.Complete(line, line.Length)).Items |> List.map (fun c -> c.Text)

    /// The smoke test the foundation leaves: the stream fills in the rest.
    [<TestMethod>]
    member _.AVariableCompletes() =
        let harness = seeded ()

        assertContains "$row" (texts harness "ls | where $r")
