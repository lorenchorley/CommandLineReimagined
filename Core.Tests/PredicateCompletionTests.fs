namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// Inside a predicate: operands, operators and values (stream E).
[<TestClass>]
type PredicateCompletionTests() =

    let texts (harness: Harness) (line: string) =
        Async.RunSynchronously(harness.Session.Complete(line, line.Length)).Items |> List.map (fun c -> c.Text)

    /// The smoke test the foundation leaves: the stream fills in the rest.
    [<TestMethod>]
    member _.AnOperatorCompletes() =
        let harness = seeded ()

        assertContains "eq" (texts harness "ls | where $row.kind e")
