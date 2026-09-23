namespace CommandLineReimagined.Core.Tests

open System.Threading
open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// What flows into a stage (stream F, decision 0031).
[<TestClass>]
type ShapeTests() =

    /// The smoke test the foundation leaves: with nothing run, the columns are a
    /// listing's, which is what completion answered before Phase 8.
    [<TestMethod>]
    member _.WithNothingRunTheColumnsAreAListingsColumns() =
        let harness = seeded ()

        let source =
            { Projection = harness.Projection
              Preview = fun _ _ -> async.Return None
              Cancel = CancellationToken.None }

        let stage =
            { Spec = None
              Name = "where"
              Index = 1
              Upstream = Some "ls"
              Written = [] }

        let shape = Async.RunSynchronously(Shape.ofUpstream source stage)

        for column in Table.recordColumns do
            assertContains column (shape.Columns |> List.map fst)
