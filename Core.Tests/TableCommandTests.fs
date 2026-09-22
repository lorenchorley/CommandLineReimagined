namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>The table functions, through whole command lines.</summary>
/// <remarks>
/// Run as lines rather than as functions, because half of what these have to get right
/// is binding: `sort name desc` has to read as it looks, the table has to arrive
/// through the pipe, and `select name kind` has to take two columns rather than
/// reporting one argument too many.
/// </remarks>
[<TestClass>]
type TableCommandTests() =

    /// A folder of notes with attributes to query, on top of the standard seed.
    let notes () =
        let harness = seeded ()
        harness.Run "mkdir journal" |> ignore
        harness.Run "cd journal" |> ignore
        harness.Run "save <note name=monday mood=good tag=work/>" |> ignore
        harness.Run "save <note name=tuesday mood=tired tag=work/>" |> ignore
        harness.Run "save <note name=saturday mood=great tag=home/>" |> ignore
        harness

    // ------------------------------------------------------------------ where

    [<TestMethod>]
    member _.WhereKeepsTheRowsThePredicateIsTrueFor() =
        let harness = notes ()

        Assert.AreEqual<string>("monday tuesday", harness.Names "ls | where $row.tag eq work")

    [<TestMethod>]
    member _.WhereCombinesWithAndAndOr() =
        let harness = notes ()

        Assert.AreEqual<string>(
            "monday",
            harness.Names "ls | where $row.tag eq work and $row.mood eq good")

        Assert.AreEqual<string>(
            "monday saturday",
            harness.Names "ls | where $row.mood eq good or $row.mood eq great")

    [<TestMethod>]
    member _.WhereUnderstandsNot() =
        let harness = notes ()

        Assert.AreEqual<string>("saturday", harness.Names "ls | where not $row.tag eq work")

    /// Decision 0008: `$row` is bound in a frame of its own, so a variable of that name
    /// outside the predicate is left exactly as it was.
    [<TestMethod>]
    member _.ThePredicatesRowIsLocalToIt() =
        let harness = notes ()
        harness.Run "set row mine" |> ignore

        harness.Run "ls | where $row.tag eq work" |> ignore

        Assert.AreEqual<Value option>(Some(Value.Text "mine"), harness.Variable "row")

    [<TestMethod>]
    member _.AColumnAGapMakesAFalsePredicate() =
        let harness = notes ()

        Assert.AreEqual<string>("", harness.Names "ls | where $row.nothing eq anything")

    // ----------------------------------------------------------------- select

    [<TestMethod>]
    member _.SelectKeepsTheNamedColumnsInOrder() =
        let harness = notes ()
        let table = harness.Table "ls | select mood name"

        CollectionAssert.AreEqual([| "mood"; "name" |], Table.names table |> Array.ofList)

    [<TestMethod>]
    member _.SelectNamesAColumnThatIsNotThere() =
        let harness = notes ()

        Assert.AreEqual<string>(
            "'select' has no column named 'nowhere'.",
            harness.Error "ls | select nowhere")

    /// Decision 0021: a rest parameter never takes the pipe, so `select` with nothing
    /// written says what it needs rather than silently answering an empty table.
    [<TestMethod>]
    member _.SelectWithNoColumnsSaysSo() =
        let harness = notes ()

        Assert.AreEqual<string>("'select' needs at least one column.", harness.Error "ls | select")

    // ------------------------------------------------------------------- sort

    [<TestMethod>]
    member _.SortOrdersByAColumn() =
        let harness = notes ()

        Assert.AreEqual<string>("monday saturday tuesday", harness.Names "ls | sort name")

    [<TestMethod>]
    member _.SortDescendingIsAWordAfterTheColumn() =
        let harness = notes ()

        Assert.AreEqual<string>("tuesday saturday monday", harness.Names "ls | sort name desc")

    /// A number column sorts as numbers. As text, `9` comes after `100`.
    [<TestMethod>]
    member _.SortIsNumericOnNumbers() =
        let harness = seeded ()
        harness.Run "mkdir sizes" |> ignore
        harness.Run "cd sizes" |> ignore
        harness.Run "write small.txt 123456789" |> ignore
        harness.Run "write big.txt 1234567890123" |> ignore

        Assert.AreEqual<string>("small.txt big.txt", harness.Names "ls | sort size")

    // ------------------------------------------------------ take, skip, count

    [<TestMethod>]
    member _.TakeAndSkipDivideTheRows() =
        let harness = notes ()

        Assert.AreEqual<string>("monday saturday", harness.Names "ls | sort name | take 2")
        Assert.AreEqual<string>("tuesday", harness.Names "ls | sort name | skip 2")

    /// More than there are is everything, not a failure.
    [<TestMethod>]
    member _.TakingMoreThanThereIsTakesWhatThereIs() =
        let harness = notes ()

        Assert.AreEqual<int>(3, List.length (harness.Table "ls | take 99").Rows)
        Assert.AreEqual<int>(0, List.length (harness.Table "ls | skip 99").Rows)

    [<TestMethod>]
    member _.CountCountsTheRows() =
        let harness = notes ()

        Assert.AreEqual<Value>(Value.Number 3.0, harness.Run "ls | count")
        Assert.AreEqual<Value>(Value.Number 2.0, harness.Run "ls | where $row.tag eq work | count")

    // -------------------------------------------------------- first and last

    [<TestMethod>]
    member _.FirstAndLastAnswerARow() =
        let harness = notes ()

        Assert.AreEqual<string>("monday", Value.display (Expr.readMember "name" (harness.Run "ls | sort name | first")))
        Assert.AreEqual<string>("tuesday", Value.display (Expr.readMember "name" (harness.Run "ls | sort name | last")))

    /// Nothing there is an answer, not a failure: it is what `??` will default in
    /// Phase 5.
    [<TestMethod>]
    member _.FirstOfNothingIsNone() =
        let harness = notes ()

        Assert.AreEqual<Value>(Value.None, harness.Run "ls | where $row.tag eq nowhere | first")

    // ------------------------------------------------- distinct, group, shape

    [<TestMethod>]
    member _.DistinctOnAColumnGivesItsValues() =
        let harness = notes ()

        Assert.AreEqual<string>("work home", harness.Column "ls | distinct tag" "tag" |> String.concat " ")

    [<TestMethod>]
    member _.GroupGathersTheRowsThatShareAValue() =
        let harness = notes ()
        let table = harness.Table "ls | group tag"

        CollectionAssert.AreEqual([| "key"; "rows" |], Table.names table |> Array.ofList)
        Assert.AreEqual<int>(2, List.length table.Rows)

        match Table.cell table "rows" (List.head table.Rows) with
        | Value.Table inner -> Assert.AreEqual<int>(2, List.length inner.Rows)
        | other -> Assert.Fail(sprintf "Expected a nested table, got %A" other)

    [<TestMethod>]
    member _.ColumnsDescribesTheTable() =
        let harness = notes ()
        let table = harness.Table "ls | columns"

        let types = table.Rows |> List.map (fun row -> Value.display (Table.cell table "type" row))

        assertContains "file" types
        assertContains "number" types

    [<TestMethod>]
    member _.RowsAreObjects() =
        let harness = notes ()

        match harness.Run "ls | rows" with
        | Value.List items -> Assert.AreEqual<int>(3, items.Length)
        | other -> Assert.Fail(sprintf "Expected a list, got %A" other)

    // --------------------------------------------------------------- coercion

    /// Decision 0009: a table-shaped tag is read as a table wherever one is expected.
    [<TestMethod>]
    member _.ATableShapedTagIsReadAsATable() =
        let harness = seeded ()

        Assert.AreEqual<Value>(
            Value.Number 2.0,
            harness.Run "<items><item sku=A1/><item sku=B2/></items> | count")

    [<TestMethod>]
    member _.ATagThatIsNotTableShapedSaysWhichChild() =
        let harness = seeded ()

        StringAssert.Contains(
            harness.Error "<items><item sku=A1/><other sku=B2/></items> | count",
            "child 2")

    [<TestMethod>]
    member _.TableIsTheExplicitCoercion() =
        let harness = seeded ()

        match harness.Run "<items><item sku=A1/></items> | table" with
        | Value.Table table -> CollectionAssert.AreEqual([| "sku" |], Table.names table |> Array.ofList)
        | other -> Assert.Fail(sprintf "Expected a table, got %A" other)

    [<TestMethod>]
    member _.SomethingThatIsNotATableSaysSo() =
        let harness = seeded ()

        Assert.AreEqual<string>("'count' needs a table, not text.", harness.Error "echo hello | count")

    // ------------------------------------------------------------- expressions

    /// An expression only means something to a parameter that asked for one.
    [<TestMethod>]
    member _.ACommandThatTakesNoPredicateRefusesAnExpression() =
        let harness = seeded ()

        Assert.AreEqual<string>(
            "'echo' takes a value for 'text', not an expression.",
            harness.Error "echo $a eq b")

    /// Decision 0019: a reserved word is never an argument.
    [<TestMethod>]
    member _.AReservedWordIsASyntaxError() =
        let harness = seeded ()

        let fault = harness.Fail "echo eq"

        Assert.AreEqual<FaultKind>(Syntax, fault.Kind)

        // The rule knows why, so the fault says why rather than "could not parse".
        Assert.AreEqual<string>("'eq' is an operator; write \"eq\" to pass it as text", fault.Message)
        Assert.AreEqual<string>("eq", harness.Text "echo \"eq\"")

    // -------------------------------------------------------------------- help

    /// `help` is a command now, so it can be piped like anything else.
    [<TestMethod>]
    member _.HelpIsATableOfCommands() =
        let harness = seeded ()
        let table = harness.Table "help | where $row.name eq set"

        Assert.AreEqual<int>(1, List.length table.Rows)

        Assert.AreEqual<string>(
            "Bind a value, or whatever was piped in, to a variable",
            Value.display (Table.cell table "description" (List.head table.Rows)))

    [<TestMethod>]
    member _.HelpChangesNothing() =
        let harness = seeded ()
        harness.Run "help" |> ignore

        Assert.AreEqual<string list>([ "seed" ], harness.History())

    /// <summary>Descending keeps the ties in the order they arrived.</summary>
    /// <remarks>
    /// Reversing an ascending sort would put them back to front, so two sorts in a row
    /// would not compose: `sort name | sort size desc` has to leave the equal sizes in
    /// name order.
    /// </remarks>
    [<TestMethod>]
    member _.SortDescendingIsStable() =
        let harness = seeded ()

        Assert.AreEqual<string>(
            "readme.txt documents examples projects",
            harness.Names "ls | sort size desc")
