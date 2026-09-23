namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// Arguments by what their parameter takes, and the signature (stream D).
[<TestClass>]
type ArgumentCompletionTests() =

    let texts (harness: Harness) (line: string) =
        Async.RunSynchronously(harness.Session.Complete(line, line.Length)).Items |> List.map (fun c -> c.Text)

    /// The smoke test the foundation leaves: the stream fills in the rest.
    [<TestMethod>]
    member _.AnArgumentCompletes() =
        let harness = seeded ()

        assertContains "readme.txt" (texts harness "cat re")
