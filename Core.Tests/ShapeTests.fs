namespace CommandLineReimagined.Core.Tests

open System
open System.Diagnostics
open System.Threading
open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// What flows into a stage (stream F, decision 0031).
[<TestClass>]
type ShapeTests() =

    /// The stage a line's cursor is in, at the end of the line, as completion reads it.
    static let stageOf (harness: Harness) (line: string) : Stage =
        match (Context.analyse harness.Session.Commands line line.Length).Place with
        | Place.Member(_, _, Some stage)
        | Place.Argument(stage, _)
        | Place.Predicate(stage, _) -> stage
        | other -> raise (AssertFailedException(sprintf "'%s' is not in a stage: %A" line other))

    /// A stage with this upstream written before it.
    static let after (upstream: string option) =
        { Spec = None
          Name = "where"
          Index = (if upstream.IsSome then 1 else 0)
          Upstream = upstream
          Written = [] }

    static let shapeOf (source: ShapeSource) (stage: Stage) = run (Shape.ofUpstream source stage)

    /// The session's own source, with its preview counted.
    static let counted (harness: Harness) =
        let runs = ref 0
        let source = harness.Session.Shapes CancellationToken.None

        let source =
            { source with
                Preview =
                    fun line cancel ->
                        Interlocked.Increment(&runs.contents) |> ignore
                        source.Preview line cancel }

        source, runs

    static let columns (shape: Shape) = shape.Columns |> List.map fst

    static let column (name: string) (shape: Shape) =
        shape.Rows
        |> Option.defaultWith (fun () -> raise (AssertFailedException "The upstream was not run."))
        |> List.map (fun row -> Map.tryFind name row |> Option.map Value.display |> Option.defaultValue "")

    static let listing (harness: Harness) = columns (Shape.ofListing harness.Session.Projection)

    /// A source whose preview never answers, and counts how often it is asked.
    static let silent (harness: Harness) =
        let asked = ref 0

        { Projection = harness.Session.Projection
          Preview =
            fun _ _ ->
                Interlocked.Increment(&asked.contents) |> ignore
                Async.FromContinuations(fun _ -> ())
          Cancel = CancellationToken.None
          Cache = ShapeCache() },
        asked

    /// <summary>A preview that answers when the test says so, on the test's thread.</summary>
    /// <remarks>
    /// The continuation is called directly rather than through a task, so whatever the
    /// answer causes has happened by the time the call returns.
    /// </remarks>
    static let held () =
        let resume = ref (fun (_: Value option) -> ())

        (fun (value: Value option) -> resume.Value value),
        Async.FromContinuations(fun (answer, _, _) -> resume.Value <- answer)

    /// The smoke test the foundation leaves: with nothing run, the columns are a
    /// listing's, which is what completion answered before Phase 8.
    [<TestMethod>]
    member _.WithNothingRunTheColumnsAreAListingsColumns() =
        let harness = seeded ()

        let source =
            { Projection = harness.Projection
              Preview = fun _ _ -> async.Return None
              Cancel = CancellationToken.None
              Cache = ShapeCache() }

        let stage =
            { Spec = None
              Name = "where"
              Index = 1
              Upstream = Some "ls"
              Written = [] }

        let shape = Async.RunSynchronously(Shape.ofUpstream source stage)

        for column in Table.recordColumns do
            assertContains column (shape.Columns |> List.map fst)

    // ------------------------------------------------------------ Finding 6

    /// `ls documents | where $row.` is offered the columns of `documents`, not of the
    /// folder the session is in: its attribute is there, and its rows are its own.
    [<TestMethod>]
    member _.LsDocumentsGivesTheColumnsAndRowsOfDocuments() =
        let harness = seeded ()
        harness.Run "attr documents/notes.txt topic=work" |> ignore

        let shape = shapeOf (harness.Session.Shapes CancellationToken.None) (stageOf harness "ls documents | where $row.")

        for name in Table.recordColumns @ [ "topic" ] do
            assertContains name (columns shape)

        assertDoesNotContain "topic" (listing harness)
        Assert.AreEqual<string list>([ "notes.txt" ], column "name" shape)
        Assert.AreEqual<string list>([ "work" ], column "topic" shape)

        Assert.AreEqual<ColumnType>(
            TextCol,
            shape.Columns |> List.find (fun (name, _) -> name = "kind") |> snd)

    /// `ls | select name | where $row.` is offered the one column `select` kept.
    [<TestMethod>]
    member _.LsSelectNameGivesTheOneColumnSelectKept() =
        let harness = seeded ()

        let shape = shapeOf (harness.Session.Shapes CancellationToken.None) (stageOf harness "ls | select name | where $row.")

        Assert.AreEqual<string list>([ "name" ], columns shape)
        Assert.AreEqual<string list>(harness.Column "ls" "name", column "name" shape)

    /// A CSV file read through `from-csv` gives its header as columns, typed as the
    /// command types them, and its values as rows: what finding 24 offers after `eq`.
    [<TestMethod>]
    member _.FromCsvGivesTheFilesColumnsAndValues() =
        let harness =
            Harness(Seed.standardFiles @ [ { Name = "stock.csv"; Folder = "/"; Content = Some "sku,qty\nA1,120\nB2,12\n" } ])

        let shape = shapeOf (harness.Session.Shapes CancellationToken.None) (stageOf harness "from-csv stock.csv | where $row.")

        Assert.AreEqual<(string * ColumnType) list>([ "sku", TextCol; "qty", NumberCol ], shape.Columns)
        Assert.AreEqual<string list>([ "A1"; "B2" ], column "sku" shape)
        Assert.AreEqual<string list>([ "120"; "12" ], column "qty" shape)

    /// Finding 12's columns: `ls | sort ` is a stage like any other.
    [<TestMethod>]
    member _.AnArgumentStageIsGivenWhatFlowsIntoIt() =
        let harness = seeded ()

        let shape = shapeOf (harness.Session.Shapes CancellationToken.None) (stageOf harness "ls documents | select name | sort ")

        Assert.AreEqual<string list>([ "name" ], columns shape)
        Assert.AreEqual<string list>([ "notes.txt" ], column "name" shape)

    // ----------------------------------------------------------- What it reads

    /// A tag and a list of tags go through the coercion `table` applies (decision 0009).
    [<TestMethod>]
    member _.ATagIsReadAsTheTableItWouldBe() =
        let tag =
            Tag.create "t" [] [ Value.Object(Tag.create "r" [ "a", Value.Number 1.0; "b", Value.Text "x" ] []); Value.Object(Tag.create "r" [ "a", Value.Number 2.0 ] []) ]

        let shape = Shape.ofValue (Value.Object tag)

        Assert.AreEqual<(string * ColumnType) list>([ "a", NumberCol; "b", TextCol ], shape.Columns)
        Assert.AreEqual<string list>([ "x"; "" ], column "b" shape)

        let list = Shape.ofValue (Value.List tag.Children)
        Assert.AreEqual<(string * ColumnType) list>(shape.Columns, list.Columns)

    /// A tag that is not table-shaped, and anything that is not a table, has no columns.
    [<TestMethod>]
    member _.WhatIsNotATableHasNoColumns() =
        let tree = Tag.create "t" [] [ Value.Object(Tag.create "r" [] [ Value.Text "deep" ]) ]

        Assert.AreEqual<Shape>(Shape.none, Shape.ofValue (Value.Object tree))
        Assert.AreEqual<Shape>(Shape.none, Shape.ofValue (Value.Number 4.0))

        let harness = seeded ()
        let shape = shapeOf (harness.Session.Shapes CancellationToken.None) (stageOf harness "ls | count | where $row.")

        Assert.AreEqual<Shape>(Shape.none, shape)

    /// At the head of a pipeline nothing flows in, and the stage lists the folder.
    [<TestMethod>]
    member _.WithNoUpstreamTheColumnsAreTheListings() =
        let harness = seeded ()
        let source, runs = counted harness

        let shape = shapeOf source (after None)

        Assert.AreEqual<string list>(listing harness, columns shape)
        Assert.AreEqual<Map<string, Value> list option>(None, shape.Rows)
        Assert.AreEqual<int>(0, runs.Value)

    /// A lone variable upstream is read from the projection, and nothing runs.
    [<TestMethod>]
    member _.ALoneVariableIsReadAndNothingRuns() =
        let harness = seeded ()
        harness.Run "ls documents | set files" |> ignore
        let source, runs = counted harness

        let shape = shapeOf source (after (Some "$files"))

        Assert.AreEqual<int>(0, runs.Value)
        Assert.AreEqual<string list>([ "notes.txt" ], column "name" shape)

    /// A variable at the head hands its value to the stages after it, without being
    /// looked up by anything but the preview's own scope.
    [<TestMethod>]
    member _.AVariableAtTheHeadFeedsTheStagesAfterIt() =
        let harness = seeded ()
        harness.Run "ls documents | set files" |> ignore

        let shape = shapeOf (harness.Session.Shapes CancellationToken.None) (after (Some "$files | select name"))

        Assert.AreEqual<string list>([ "name" ], columns shape)
        Assert.AreEqual<string list>([ "notes.txt" ], column "name" shape)

    /// A variable that is not there is not run: the line could only fail.
    [<TestMethod>]
    member _.AnUnknownVariableFallsBackWithoutRunning() =
        let harness = seeded ()
        let source, runs = counted harness

        Assert.AreEqual<string list>(listing harness, columns (shapeOf source (after (Some "$nothing | select name"))))
        Assert.AreEqual<string list>(listing harness, columns (shapeOf source (after (Some "$nothing"))))
        Assert.AreEqual<int>(0, runs.Value)

    // ---------------------------------------------------------------- Fallback

    /// A line that would write is refused, falls back to the listing, and leaves no trace.
    [<TestMethod>]
    member _.AnUpstreamThatWritesFallsBackAndWritesNothing() =
        let harness = seeded ()
        let before = harness.History()

        let shape = shapeOf (harness.Session.Shapes CancellationToken.None) (stageOf harness "mkdir scratch | where $row.")

        Assert.AreEqual<string list>(listing harness, columns shape)
        Assert.AreEqual<Map<string, Value> list option>(None, shape.Rows)
        Assert.IsFalse(harness.Exists "scratch", "The preview made the folder.")
        Assert.AreEqual<string list>(before, harness.History())

    /// An upstream that fails falls back, never an error.
    [<TestMethod>]
    member _.AnUpstreamThatFailsFallsBack() =
        let harness = seeded ()

        let shape = shapeOf (harness.Session.Shapes CancellationToken.None) (stageOf harness "read nowhere.txt | where $row.")

        Assert.AreEqual<string list>(listing harness, columns shape)

    /// A preview that never answers is answered for when the budget runs out.
    [<TestMethod>]
    member _.ASlowUpstreamFallsBackInsideTheBudget() =
        let harness = seeded ()
        let source, asked = silent harness
        let clock = Stopwatch.StartNew()

        let shape = shapeOf source (after (Some "ls"))

        clock.Stop()
        Assert.AreEqual<int>(1, asked.Value)
        Assert.AreEqual<string list>(listing harness, columns shape)
        Assert.AreEqual<Map<string, Value> list option>(None, shape.Rows)

        Assert.IsTrue(
            clock.Elapsed >= Shape.budget - TimeSpan.FromMilliseconds 20.0,
            sprintf "Answered after %O, before the budget was spent." clock.Elapsed)

        Assert.IsTrue(clock.Elapsed < TimeSpan.FromSeconds 1.0, sprintf "Answered only after %O." clock.Elapsed)

    /// A run that misses the budget goes on, and the next keystroke finds its answer.
    [<TestMethod>]
    member _.ALateAnswerIsKeptForTheNextKeystroke() =
        let harness = seeded ()
        let answer, later = held ()
        let runs = ref 0

        let source =
            { harness.Session.Shapes CancellationToken.None with
                Preview =
                    fun _ _ ->
                        Interlocked.Increment(&runs.contents) |> ignore
                        later }

        let first = shapeOf source (after (Some "ls documents"))
        Assert.AreEqual<Map<string, Value> list option>(None, first.Rows)

        answer (Some(harness.Run "ls documents"))
        Assert.AreEqual<int>(1, source.Cache.Count)

        let second = shapeOf source (after (Some "ls documents"))
        Assert.AreEqual<string list>([ "notes.txt" ], column "name" second)
        Assert.AreEqual<int>(1, runs.Value)

    /// A preview that answers inside the budget, after waiting, is used.
    [<TestMethod>]
    member _.AnUpstreamThatWaitsInsideTheBudgetIsUsed() =
        let harness = seeded ()
        let real = harness.Session.Shapes CancellationToken.None

        let source =
            { real with
                Preview =
                    fun line cancel ->
                        async {
                            do! Async.Sleep 20
                            return! real.Preview line cancel
                        } }

        Assert.AreEqual<string list>([ "notes.txt" ], column "name" (shapeOf source (after (Some "ls documents"))))

    /// A request the next keystroke has already cancelled runs nothing.
    [<TestMethod>]
    member _.ACancelledRequestRunsNothing() =
        let harness = seeded ()
        let source, runs = counted harness
        use cancelled = new CancellationTokenSource()
        cancelled.Cancel()

        let shape = shapeOf { source with Cancel = cancelled.Token } (after (Some "ls documents"))

        Assert.AreEqual<string list>(listing harness, columns shape)
        Assert.AreEqual<int>(0, runs.Value)

    /// The next keystroke cancels a run in flight: its request falls back, and the
    /// cancelled run is not remembered.
    [<TestMethod>]
    member _.ACancelledRunFallsBackAndIsNotKept() =
        let harness = seeded ()
        use keystroke = new CancellationTokenSource()
        let real = harness.Session.Shapes keystroke.Token

        let source =
            { real with
                Preview =
                    fun line cancel ->
                        async {
                            keystroke.Cancel()
                            return! real.Preview line cancel
                        } }

        let shape = shapeOf source (after (Some "ls documents"))

        Assert.AreEqual<string list>(listing harness, columns shape)
        Assert.AreEqual<int>(0, real.Cache.Count)

    // ------------------------------------------------------------------- Cache

    /// Typing inside the last stage asks about the same upstream, which runs once.
    [<TestMethod>]
    member _.TypingInTheLastStageDoesNotRunTheUpstreamAgain() =
        let harness = seeded ()
        let source, runs = counted harness

        let first = shapeOf source (stageOf harness "ls documents | where $row.")
        let second = shapeOf source (stageOf harness "ls documents | where $row.ki")
        let third = shapeOf source (stageOf harness "ls documents | where $row.kind eq ")

        Assert.AreEqual<int>(1, runs.Value)
        Assert.AreEqual<Shape>(first, second)
        Assert.AreEqual<Shape>(first, third)
        Assert.AreEqual<int>(1, source.Cache.Count)

    /// A refusal is remembered too: the line is no more read-only on the next keystroke.
    [<TestMethod>]
    member _.ARefusalIsRememberedToo() =
        let harness = seeded ()
        let source, runs = counted harness

        shapeOf source (after (Some "mkdir scratch")) |> ignore
        shapeOf source (after (Some "mkdir scratch")) |> ignore

        Assert.AreEqual<int>(1, runs.Value)

    /// A commit clears the cache, and the next request runs the upstream again and sees
    /// what the commit changed.
    [<TestMethod>]
    member _.ACommitClearsTheCache() =
        let harness = seeded ()
        let source, runs = counted harness
        let stage = stageOf harness "ls documents | where $row."

        Assert.AreEqual<string list>([ "notes.txt" ], column "name" (shapeOf source stage))
        Assert.AreEqual<int>(1, source.Cache.Count)

        harness.Run "mkdir documents/scratch" |> ignore

        Assert.AreEqual<int>(0, source.Cache.Count)

        let source, again = counted harness
        Assert.AreEqual<string list>([ "notes.txt"; "scratch" ], column "name" (shapeOf source stage) |> List.sort)
        Assert.AreEqual<int>(1, again.Value)
        Assert.AreEqual<int>(1, runs.Value)

    /// An answer to a question asked before a commit is not kept after it.
    [<TestMethod>]
    member _.AnAnswerFromBeforeACommitIsNotKept() =
        let harness = seeded ()
        let answer, later = held ()

        let source =
            { harness.Session.Shapes CancellationToken.None with
                Preview = fun _ _ -> later }

        shapeOf source (after (Some "ls documents")) |> ignore
        harness.Run "mkdir documents/scratch" |> ignore
        answer (Some(harness.Run "ls documents"))

        Assert.AreEqual<int>(0, source.Cache.Count)

    /// The cache keeps shapes for one store sequence number only.
    [<TestMethod>]
    member _.TheCacheIsKeyedByTheSequenceNumber() =
        let cache = ShapeCache()
        let shape = Shape.ofValue (Value.Table(Table.ofColumns [ "a" ] [ [ Value.Number 1.0 ] ]))

        cache.Add(cache.Generation, 3L, "ls", shape)
        Assert.AreEqual<Shape option>(Some shape, cache.TryFind(3L, "ls"))
        Assert.AreEqual<Shape option>(None, cache.TryFind(4L, "ls"))

        cache.Add(cache.Generation, 4L, "ls documents", shape)
        Assert.AreEqual<Shape option>(None, cache.TryFind(3L, "ls"))
        Assert.AreEqual<int>(1, cache.Count)

        // A late answer from an older sequence number does not displace the newer.
        cache.Add(cache.Generation, 3L, "ls", shape)
        Assert.AreEqual<Shape option>(Some shape, cache.TryFind(4L, "ls documents"))
        Assert.AreEqual<int>(1, cache.Count)

    // ------------------------------------------------ Phase 11: the value kept

    /// A shape keeps what the upstream answered, whatever it was, for a provider that
    /// reads more than a table: a tree has no columns and is still there (decision 0049).
    [<TestMethod>]
    member _.AShapeKeepsTheValueItWasMadeFrom() =
        let tree = Tag.create "t" [] [ Value.Object(Tag.create "r" [] [ Value.Text "deep" ]) ]
        let table = Value.Table(Table.ofColumns [ "a" ] [ [ Value.Number 1.0 ] ])

        Assert.AreEqual<Value option>(Some(Value.Object tree), (Shape.ofValue (Value.Object tree)).Value)
        Assert.AreEqual<Value option>(Some(Value.Number 4.0), (Shape.ofValue (Value.Number 4.0)).Value)
        Assert.AreEqual<Value option>(Some table, (Shape.ofValue table).Value)

    /// The value is carried, not compared: a shape is its columns and rows.
    [<TestMethod>]
    member _.TheValueIsNotPartOfTheShapesEquality() =
        Assert.AreEqual<Shape>(Shape.none, { Shape.none with Value = Some(Value.Number 4.0) })
        Assert.AreEqual<Shape>(Shape.ofValue (Value.Text "a"), Shape.ofValue (Value.Text "b"))

    /// What the listing's shape is made from was never run, so it has no value.
    [<TestMethod>]
    member _.AListingHasNoValue() =
        let harness = seeded ()

        Assert.AreEqual<Value option>(None, (Shape.ofListing harness.Session.Projection).Value)
        Assert.AreEqual<Value option>(None, (shapeOf (harness.Session.Shapes CancellationToken.None) (after None)).Value)

    /// An upstream that is run keeps its answer, and so does a lone variable, read
    /// without running anything.
    [<TestMethod>]
    member _.AnUpstreamRunOrReadKeepsItsValue() =
        let harness = seeded ()
        harness.Run "set d <library><book title=dune/></library>" |> ignore
        let source = harness.Session.Shapes CancellationToken.None

        match (shapeOf source (after (Some "$d"))).Value with
        | Some(Value.Object tag) -> Assert.AreEqual<string>("library", tag.TypeName)
        | other -> Assert.Fail(sprintf "%A" other)

        match (shapeOf source (after (Some "$d | pick book"))).Value with
        | Some(Value.Table table) -> Assert.AreEqual<int>(1, table.Rows.Length)
        | other -> Assert.Fail(sprintf "%A" other)
