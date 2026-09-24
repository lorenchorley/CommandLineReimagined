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
        harness.Run "in journal" |> ignore
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

    // ------------------------------------------- a question about the row (0033)

    /// Finding 26: comparing two words is the same for every row, so it is a binding
    /// fault that names the words as the columns they were likely meant to be.
    [<TestMethod>]
    member _.APredicateThatNeverReadsTheRowIsABindingFault() =
        let harness = seeded ()
        let fault = harness.Fail "ls | where kind eq folder"

        Assert.AreEqual<FaultKind>(Binding, fault.Kind)

        Assert.AreEqual<string>(
            "kind eq folder never reads $row, so it is the same for every row. Did you mean $row.kind eq folder?",
            fault.Message)

    [<TestMethod>]
    member _.EveryBareWordComparedIsNamedAsAColumn() =
        let harness = seeded ()

        Assert.AreEqual<string>(
            "kind eq folder and size gt 3 never reads $row, so it is the same for every row. Did you mean $row.kind eq folder and $row.size gt 3?",
            harness.Error "ls | where kind eq folder and size gt 3")

        Assert.AreEqual<string>(
            "3 lt size never reads $row, so it is the same for every row. Did you mean 3 lt $row.size?",
            harness.Error "ls | where 3 lt size")

        Assert.AreEqual<string>(
            "not done never reads $row, so it is the same for every row. Did you mean not $row.done?",
            harness.Error "ls | where not done")

    /// With no bare word to read as a column, the fault says what is wrong and no more.
    [<TestMethod>]
    member _.APredicateOverAVariableAloneHasNoSuggestion() =
        let harness = seeded ()
        harness.Run "set x 1" |> ignore

        Assert.AreEqual<string>(
            "$x eq 1 never reads $row, so it is the same for every row.",
            harness.Error "ls | where $x eq 1")

    /// A nested pipeline runs once for the line, so a predicate over only its value
    /// asks nothing of the rows. The fault quotes the pipeline as it was written.
    [<TestMethod>]
    member _.ANestedPipelineIsNotTheRow() =
        let harness = seeded ()

        Assert.AreEqual<string>(
            "(ls | count) gt 3 never reads $row, so it is the same for every row.",
            harness.Error "ls | where (ls | count) gt 3")

        Assert.AreEqual<int>(1, (harness.Table "ls | where $row.size gt (ls | count)").Rows.Length)

    /// A fault like any other, so `else` recovers from it.
    [<TestMethod>]
    member _.ThePredicateFaultIsRecoverable() =
        let harness = seeded ()

        Assert.AreEqual<string>("recovered", harness.Text "ls | where kind eq folder else echo recovered")

    /// Finding 25: a column read bare that is not true or false, named with its value
    /// on the first row it was seen on, and the comparison to write instead.
    [<TestMethod>]
    member _.APredicateThatIsNotTrueOrFalseIsAnInvalidFault() =
        let harness = seeded ()
        let fault = harness.Fail "ls | where $row.kind"

        Assert.AreEqual<FaultKind>(Invalid, fault.Kind)

        Assert.AreEqual<string>(
            "$row.kind is text (folder), not true or false. Compare it: $row.kind eq folder.",
            fault.Message)

    /// A bare word as the whole predicate is a plain operand, so it binds; on the first
    /// row it is found not to be a question, and the word is named as a column.
    [<TestMethod>]
    member _.ABareWordIsNotTrueOrFalse() =
        let harness = seeded ()

        Assert.AreEqual<string>(
            "done is text (done), not true or false. Did you mean $row.done?",
            harness.Error "ls | where done")

    /// A boolean read bare stays valid (decision 0033): `history`'s `undone` is one.
    [<TestMethod>]
    member _.ABooleanReadBareIsAPredicate() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore
        harness.Run "mkdir beta" |> ignore
        harness.Run "undo" |> ignore

        Assert.AreEqual<string>("1", harness.Text "history | where $row.undone | count")

        Assert.AreEqual<string>(
            harness.Text "history | where $row.undone eq false | count",
            harness.Text "history | where not $row.undone | count")

    /// A gap is not an answer of the wrong kind (decision 0009): a row without the
    /// column is skipped, as a comparison with a gap is.
    [<TestMethod>]
    member _.AGapReadBareSkipsTheRow() =
        let harness = notes ()

        Assert.AreEqual<string>("", harness.Names "ls | where $row.nothing")

    /// `attr x done=true` stores the word, not a boolean, and read bare it answers the
    /// question it plainly asks (decision 0034). `false` answers no, in any case.
    [<TestMethod>]
    member _.AWordThatReadsTrueOrFalseIsAnAnswer() =
        let harness = notes ()
        harness.Run "attr monday done=true" |> ignore
        harness.Run "attr tuesday done=FALSE" |> ignore

        Assert.AreEqual<string>("monday", harness.Names "ls | where $row.done")
        Assert.AreEqual<string>("tuesday", harness.Names "ls | where $row.done eq FALSE")

    /// Each operand of `not`, `and` and `or` is held to the same rule as the whole
    /// predicate (decision 0034), so text is no longer quietly false inside one.
    [<TestMethod>]
    member _.AnOperandThatIsNotTrueOrFalseIsAnInvalidFault() =
        let harness = seeded ()
        let fault = harness.Fail "ls | where not $row.kind"

        Assert.AreEqual<FaultKind>(Invalid, fault.Kind)

        Assert.AreEqual<string>(
            "$row.kind is text (folder), not true or false. Compare it: $row.kind eq folder.",
            fault.Message)

        Assert.AreEqual<string>(
            "$row.kind is text (folder), not true or false. Compare it: $row.kind eq folder.",
            harness.Error "ls | where $row.kind eq text or $row.kind")

    /// Short circuiting still holds: an `and` whose left is false never reads its right,
    /// so the right is not held to the rule on that row.
    [<TestMethod>]
    member _.AnOperandNeverReadIsNotChecked() =
        let harness = seeded ()

        Assert.AreEqual<string>("", harness.Names "ls | where $row.kind eq nothing and $row.kind")
        Assert.AreEqual<int>(5, (harness.Table "ls | where $row.size ge 0 or $row.kind").Rows.Length)

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

    /// The switch written bare means the same as the word.
    [<TestMethod>]
    member _.SortDescendingIsAlsoASwitch() =
        let harness = notes ()

        Assert.AreEqual<string>("tuesday saturday monday", harness.Names "ls | sort name -desc")

    /// Any word used to count as descending, so `asc` sorted the wrong way.
    [<TestMethod>]
    member _.SortAscendingIsTheWordAsc() =
        let harness = notes ()

        Assert.AreEqual<string>("monday saturday tuesday", harness.Names "ls | sort name asc")

    [<TestMethod>]
    member _.SortRefusesAnyOtherWord() =
        let harness = notes ()

        let fault = harness.Fail "ls | sort name up"

        Assert.AreEqual<FaultKind>(Binding, fault.Kind)
        Assert.AreEqual<string>("'sort' takes 'desc' or 'asc' for 'desc', not 'up'.", fault.Message)

    /// A number column sorts as numbers. As text, `9` comes after `100`.
    [<TestMethod>]
    member _.SortIsNumericOnNumbers() =
        let harness = seeded ()
        harness.Run "mkdir sizes" |> ignore
        harness.Run "in sizes" |> ignore
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
            "readme.txt documents examples guide projects",
            harness.Names "ls | sort size desc")
