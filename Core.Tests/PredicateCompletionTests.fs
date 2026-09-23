namespace CommandLineReimagined.Core.Tests

open System.Threading
open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>Inside a predicate: operands, operators and values (stream E).</summary>
/// <remarks>
/// Findings 22 to 24 of Phase 8. The line has been read, so each part of an expression
/// has one kind of answer: a value starts with `$row.`, `not` or `(`; a comparison
/// operator follows an operand; a column's values follow the operator; `and` and `or`
/// follow a whole comparison.
/// </remarks>
[<TestClass>]
type PredicateCompletionTests() =

    let complete (harness: Harness) (line: string) =
        Async.RunSynchronously(harness.Session.Complete(line, line.Length)).Items

    let texts (harness: Harness) (line: string) =
        complete harness line |> List.map (fun c -> c.Text)

    /// <summary>What a provider is given, with the rows `Shape` would have found.</summary>
    /// <remarks>
    /// Until stream F runs the upstream there are no rows, so the values a column holds
    /// are tested by handing the provider a shape that has them.
    /// </remarks>
    let withShape (harness: Harness) (line: string) (shape: Shape) =
        let source: ShapeSource =
            { Projection = harness.Projection
              Preview = fun _ _ -> async.Return None
              Cancel = CancellationToken.None
              Cache = ShapeCache() }

        let request = Completion.request harness.Session.Commands source line line.Length CancellationToken.None
        let request = { request with Shapes = fun _ -> async.Return shape }
        Async.RunSynchronously(Completion.complete request).Items

    let withRows (harness: Harness) (line: string) (rows: (string * Value) list list) =
        withShape harness line { Columns = []; Rows = Some(rows |> List.map Map.ofList) }

    let kinds rows = rows |> List.map (fun (k: Value) -> [ "kind", k ])

    /// The smoke test the foundation left, kept.
    [<TestMethod>]
    member _.AnOperatorCompletes() =
        let harness = seeded ()

        assertContains "eq" (texts harness "ls | where $row.kind e")

    // ---------------------------------------------------------------- Operand

    /// Finding 16's predicate half: a value starts here.
    [<TestMethod>]
    member _.APredicateStartsWithTheRowANegationOrAGroup() =
        let harness = seeded ()

        for line in [ "ls | where "; "where "; "find "; "save-view x "; "ls | where not " ] do
            Assert.AreEqual<string list>([ "$row."; "not"; "(" ], texts harness line, line)

        Assert.AreEqual<string list>(
            [ "member"; "operator"; "operator" ],
            complete harness "ls | where " |> List.map (fun c -> c.Kind))

    [<TestMethod>]
    member _.AnOperandAfterAndStartsAgain() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "$row."; "not"; "(" ], texts harness "ls | where $row.kind eq folder and ")

    /// What is written of the word narrows the offers, as everywhere else.
    [<TestMethod>]
    member _.AnOperandIsFilteredByWhatIsTyped() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "not" ], texts harness "ls | where no")
        Assert.AreEqual<int>(0, (texts harness "ls | where zz").Length)

    /// No `$` has to be written first for the predicate to be known: the old test for
    /// one kept `not` away from `cat no`, and `cat` still has only paths.
    [<TestMethod>]
    member _.APathIsNeverAPredicatePlace() =
        let harness = seeded ()
        harness.Run "cd documents" |> ignore

        Assert.AreEqual<string list>([ "notes.txt" ], texts harness "cat no")

    // ----------------------------------------------------------- AfterOperand

    /// Finding 22: after an operand, the eight comparison words and nothing else.
    [<TestMethod>]
    member _.AfterAnOperandComeTheEightComparisonOperators() =
        let harness = seeded ()
        let offered = complete harness "ls | where $row.kind "

        Assert.AreEqual<string list>([ "eq"; "ne"; "gt"; "ge"; "lt"; "le"; "like"; "has" ], offered |> List.map (fun c -> c.Text))
        Assert.IsTrue(offered |> List.forall (fun c -> c.Kind = "operator"))

    [<TestMethod>]
    member _.TheComparisonOperatorsAreFilteredByWhatIsTyped() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "lt"; "le"; "like" ], texts harness "ls | where $row.kind l")
        Assert.AreEqual<string list>([ "eq" ], texts harness "ls | where $row.kind eq")
        Assert.AreEqual<int>(0, (texts harness "ls | where $row.kind a").Length)

    /// A boolean read bare is an operand too.
    [<TestMethod>]
    member _.AfterANegatedOperandComeTheComparisonOperators() =
        let harness = seeded ()

        Assert.AreEqual<string list>(
            [ "eq"; "ne"; "gt"; "ge"; "lt"; "le"; "like"; "has" ],
            texts harness "ls | where not $row.done ")

    // -------------------------------------------------------- AfterComparison

    /// Finding 23: after a whole comparison, only the words that join another.
    [<TestMethod>]
    member _.AfterAComparisonComeAndAndOr() =
        let harness = seeded ()
        let offered = complete harness "ls | where $row.kind eq folder "

        Assert.AreEqual<string list>([ "and"; "or" ], offered |> List.map (fun c -> c.Text))
        Assert.IsTrue(offered |> List.forall (fun c -> c.Kind = "operator"))
        Assert.AreEqual<string list>([ "and" ], texts harness "ls | where $row.kind eq folder a")

    // -------------------------------------------------------- ComparisonRight

    /// <summary>Finding 24, where nothing was run: no rows, no values.</summary>
    /// <remarks>
    /// Rather than a guess, or the file names the lexical rules offered. There are no
    /// rows at the head of a pipeline, where nothing flows in; after a stage that would
    /// write, which completion never runs (decision 0031); and whenever `Shape` answers
    /// without them.
    /// </remarks>
    [<TestMethod>]
    member _.WithoutRowsTheRightHandSideOffersNothing() =
        let harness = seeded ()

        Assert.AreEqual<int>(0, (withShape harness "ls | where $row.kind eq " { Columns = []; Rows = None }).Length)
        Assert.AreEqual<int>(0, (texts harness "where $row.kind eq ").Length)
        Assert.AreEqual<int>(0, (texts harness "mkdir a | where $row.kind eq ").Length)
        Assert.AreEqual<int>(0, (texts harness "mkdir a | where $row.kind eq f").Length)

    /// Finding 24, the Acceptance row: the stages before the cursor are run, and their
    /// rows give the column's values. The seed's root holds three folders and a text.
    [<TestMethod>]
    member _.TheRealLineOffersTheColumnsValues() =
        let harness = seeded ()
        let offered = complete harness "ls | where $row.kind eq "

        Assert.AreEqual<string list>([ "folder"; "text" ], offered |> List.map (fun c -> c.Text))
        Assert.IsTrue(offered |> List.forall (fun c -> c.Kind = "value"))
        Assert.AreEqual<string list>([ "folder" ], texts harness "ls | where $row.kind eq f")
        Assert.AreEqual<string list>([ "folder*"; "text*" ], texts harness "ls | where $row.kind like ")

    /// A variable standing as the first stage (decision 0032) is read, not run, and its
    /// rows give the values just the same.
    [<TestMethod>]
    member _.AVariablesRowsOfferTheColumnsValues() =
        let harness = seeded ()
        harness.Run "ls | set files" |> ignore

        Assert.AreEqual<string list>([ "folder"; "text" ], texts harness "$files | where $row.kind eq ")

    /// Finding 24: the column's values, most frequent first.
    [<TestMethod>]
    member _.TheRightHandSideOffersTheColumnsValues() =
        let harness = seeded ()

        let rows =
            kinds [ Value.Text "text"; Value.Text "folder"; Value.Text "folder"; Value.Text "folder" ]

        let offered = withRows harness "ls | where $row.kind eq " rows

        Assert.AreEqual<string list>([ "folder"; "text" ], offered |> List.map (fun c -> c.Text))
        Assert.IsTrue(offered |> List.forall (fun c -> c.Kind = "value"))
        Assert.AreEqual<string option>(Some "3 rows", (List.head offered).Detail)

    [<TestMethod>]
    member _.TheColumnsValuesAreFilteredByWhatIsTyped() =
        let harness = seeded ()
        let rows = kinds [ Value.Text "text"; Value.Text "folder" ]

        Assert.AreEqual<string list>(
            [ "folder" ],
            withRows harness "ls | where $row.kind eq f" rows |> List.map (fun c -> c.Text))

    /// A gap is not a value anyone compares with, and one value is offered once.
    [<TestMethod>]
    member _.GapsAreNotOfferedAndValuesAreDistinct() =
        let harness = seeded ()
        let rows = kinds [ Value.Text "note"; Value.None; Value.Text "note"; Value.Empty ]

        Assert.AreEqual<string list>(
            [ "note" ],
            withRows harness "ls | where $row.kind eq " rows |> List.map (fun c -> c.Text))

    [<TestMethod>]
    member _.AtMostTwelveValuesAreOffered() =
        let harness = seeded ()
        let rows = [ 1..20 ] |> List.map (fun n -> [ "size", Value.Number(float n) ])

        Assert.AreEqual<int>(12, (withRows harness "ls | where $row.size gt " rows).Length)

    /// `like` takes a pattern, so each value is offered as one.
    [<TestMethod>]
    member _.LikeOffersEachValueAsAPattern() =
        let harness = seeded ()
        let rows = kinds [ Value.Text "folder"; Value.Text "text" ]

        Assert.AreEqual<string list>(
            [ "folder*"; "text*" ],
            withRows harness "ls | where $row.kind like " rows |> List.map (fun c -> c.Text))

    /// A value that would not read back as one word is offered quoted.
    [<TestMethod>]
    member _.AValueWithASpaceIsOfferedQuoted() =
        let harness = seeded ()
        let rows = [ [ "name", Value.Text "my notes" ] ]

        Assert.AreEqual<string list>(
            [ "\"my notes\"" ],
            withRows harness "ls | where $row.name eq " rows |> List.map (fun c -> c.Text))

    /// Only a column has values to offer: the left of `$x eq ` is not one.
    [<TestMethod>]
    member _.OnlyAColumnOffersValues() =
        let harness = seeded ()
        let rows = kinds [ Value.Text "folder" ]

        Assert.AreEqual<int>(0, (withRows harness "ls | where $x eq " rows).Length)

    // --------------------------------------------------------------------- cd

    /// `cd` with a plain operand is a path (decision 0013), so its operand names the
    /// places you can be, as it always has.
    [<TestMethod>]
    member _.CdsOperandOffersPlaces() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "documents/"; "examples/"; "guide/"; "projects/" ], texts harness "cd ")
        Assert.AreEqual<string list>([ "documents/" ], texts harness "cd doc")

    [<TestMethod>]
    member _.CdOffersASavedViewAsAPlace() =
        let harness = seeded ()
        harness.Run "save-view weekend $row.kind eq folder" |> ignore

        assertContains "weekend" (texts harness "cd we")

    /// Past its first word, `cd`'s argument is a question like any other.
    [<TestMethod>]
    member _.CdsQuestionIsCompletedAsAPredicate() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "$row."; "not"; "(" ], texts harness "cd not ")

        Assert.AreEqual<string list>(
            [ "eq"; "ne"; "gt"; "ge"; "lt"; "le"; "like"; "has" ],
            texts harness "cd $row.mood ")

        Assert.AreEqual<string list>([ "and"; "or" ], texts harness "cd $row.mood eq great ")
