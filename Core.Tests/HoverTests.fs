namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.SessionHarness

/// What a tapped token is (stream G).
[<TestClass>]
type HoverTests() =

    /// The smoke test the foundation leaves: asking never fails.
    [<TestMethod>]
    member _.AskingAboutATokenNeverFails() =
        let harness = seeded ()

        Async.RunSynchronously(harness.Session.Describe("ls | sort name", 6)) |> ignore
