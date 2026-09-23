namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness

/// <summary>Evaluating a predicate: members, comparisons and the boolean words.</summary>
/// <remarks>
/// These are the rules a user has to be able to predict. `lt` on a number column has to
/// compare numbers and on a text one has to compare words, and a gap has to answer
/// something defensible to every operator, or a predicate over a sparse table stops the
/// line instead of skipping the row.
/// </remarks>
[<TestClass>]
type ExpressionTests() =

    let row attributes = Value.Object(Tag.create "row" attributes [])

    /// A scope with `$row` bound, which is what a table function evaluates in.
    let scopeWith attributes = Scope(Map.ofList [ "row", row attributes ])

    let evaluate attributes expr = Expr.evaluate (scopeWith attributes) expr

    let isTrue attributes expr =
        match expectOk (evaluate attributes expr) with
        | Value.Boolean b -> b
        | other -> raise (AssertFailedException(sprintf "Expected a boolean, got %s." (Value.kind other)))

    let column name = Expr.Variable("row", [ name ])
    let constant value = Expr.Const value

    // ---------------------------------------------------------------- Members

    [<TestMethod>]
    member _.AMemberReadsAnAttribute() =
        let value = expectOk (evaluate [ "kind", Value.Text "folder" ] (column "kind"))

        Assert.AreEqual<Value>(Value.Text "folder", value)

    /// Decision 0009: a gap is `None`, not a failure, so one sparse row does not stop
    /// the line.
    [<TestMethod>]
    member _.AMemberThatIsNotThereIsNone() =
        Assert.AreEqual<Value>(Value.None, expectOk (evaluate [] (column "missing")))

    [<TestMethod>]
    member _.AFileAnswersItsOwnFields() =
        let file = Value.File { Id = "id"; Name = "notes.txt"; Kind = "text"; Folder = "/documents" }

        Assert.AreEqual<Value>(Value.Text "notes.txt", Expr.readMember "name" file)
        Assert.AreEqual<Value>(Value.Text "/documents/notes.txt", Expr.readMember "path" file)

    /// Decision 0032: with no row in scope, `$row` is not merely unknown, and the fault
    /// says where it does exist.
    [<TestMethod>]
    member _.AnUnknownVariableIsAFault() =
        let fault = expectFault NotFound (Expr.evaluate (Scope Map.empty) (column "kind"))

        StringAssert.Contains(fault.Message, "$row is the row a predicate is testing.")

    // ------------------------------------------------------------ Comparisons

    /// Numbers compare as numbers, including text that reads as one, which is what a
    /// column read out of an XML file arrives as.
    [<DataTestMethod>]
    [<DataRow("gt", 120.0, 50.0, true)>]
    [<DataRow("gt", 50.0, 120.0, false)>]
    [<DataRow("lt", 9.0, 100.0, true)>]
    [<DataRow("ge", 5.0, 5.0, true)>]
    [<DataRow("le", 5.0, 5.0, true)>]
    [<DataRow("eq", 5.0, 5.0, true)>]
    [<DataRow("ne", 5.0, 6.0, true)>]
    member _.NumbersCompareNumerically(op: string, left: float, right: float, expected: bool) =
        let comparison = Expr.Compare(op, constant (Value.Number left), constant (Value.Number right))

        Assert.AreEqual<bool>(expected, isTrue [] comparison)

    /// `9` against `100` is the case that says whether a column is being compared as a
    /// number or as a word: as text, `9` sorts after `100`.
    [<TestMethod>]
    member _.TextThatReadsAsANumberComparesAsOne() =
        let comparison = Expr.Compare("lt", constant (Value.Text "9"), constant (Value.Text "100"))

        Assert.IsTrue(isTrue [] comparison)

    [<TestMethod>]
    member _.WordsCompareOrdinally() =
        let comparison = Expr.Compare("lt", constant (Value.Text "apple"), constant (Value.Text "banana"))

        Assert.IsTrue(isTrue [] comparison)

    [<TestMethod>]
    member _.AValueComparesEqualToItsDisplayText() =
        let comparison = Expr.Compare("eq", column "undone", constant (Value.Text "true"))

        Assert.IsTrue(isTrue [ "undone", Value.Boolean true ] comparison)

    // ------------------------------------------------------------------ Gaps

    /// A gap is not smaller than 100, nor larger, nor equal to it. Answering false to
    /// all three is the only consistent thing to do.
    [<DataTestMethod>]
    [<DataRow("eq")>]
    [<DataRow("ne")>]
    [<DataRow("gt")>]
    [<DataRow("lt")>]
    [<DataRow("like")>]
    member _.AComparisonAgainstAGapIsFalse(op: string) =
        let comparison = Expr.Compare(op, column "missing", constant (Value.Number 100.0))

        Assert.IsFalse(isTrue [] comparison)

    [<TestMethod>]
    member _.TwoGapsAreEqual() =
        Assert.IsTrue(isTrue [] (Expr.Compare("eq", column "missing", column "absent")))

    // --------------------------------------------------------- like and has

    [<TestMethod>]
    member _.LikeIsACaseInsensitiveSubstring() =
        let comparison = Expr.Compare("like", column "name", constant (Value.Text "NOTE"))

        Assert.IsTrue(isTrue [ "name", Value.Text "notes.txt" ] comparison)

    [<TestMethod>]
    member _.LikeWithAStarIsAGlob() =
        let comparison = Expr.Compare("like", column "name", constant (Value.Text "*.txt"))

        Assert.IsTrue(isTrue [ "name", Value.Text "notes.txt" ] comparison)
        Assert.IsFalse(isTrue [ "name", Value.Text "notes.xml" ] comparison)

    [<TestMethod>]
    member _.HasFindsAnItemInAList() =
        let comparison = Expr.Compare("has", column "tags", constant (Value.Text "work"))

        Assert.IsTrue(isTrue [ "tags", Value.List [ Value.Text "home"; Value.Text "work" ] ] comparison)
        Assert.IsFalse(isTrue [ "tags", Value.List [ Value.Text "home" ] ] comparison)

    [<TestMethod>]
    member _.HasIsASubstringOnText() =
        let comparison = Expr.Compare("has", column "name", constant (Value.Text "note"))

        Assert.IsTrue(isTrue [ "name", Value.Text "notes.txt" ] comparison)

    // ------------------------------------------------------- Boolean combining

    [<TestMethod>]
    member _.AndNeedsBothSides() =
        let both =
            Expr.And(
                Expr.Compare("eq", column "kind", constant (Value.Text "note")),
                Expr.Compare("eq", column "tag", constant (Value.Text "work")))

        Assert.IsTrue(isTrue [ "kind", Value.Text "note"; "tag", Value.Text "work" ] both)
        Assert.IsFalse(isTrue [ "kind", Value.Text "note"; "tag", Value.Text "home" ] both)

    [<TestMethod>]
    member _.OrNeedsOneSide() =
        let either =
            Expr.Or(
                Expr.Compare("eq", column "kind", constant (Value.Text "note")),
                Expr.Compare("eq", column "kind", constant (Value.Text "folder")))

        Assert.IsTrue(isTrue [ "kind", Value.Text "folder" ] either)
        Assert.IsFalse(isTrue [ "kind", Value.Text "text" ] either)

    /// Short circuiting is observable: an unknown variable on the unreached side would
    /// otherwise be a fault.
    [<TestMethod>]
    member _.AndDoesNotEvaluateItsRightSideWhenTheLeftIsFalse() =
        let expr = Expr.And(Expr.Const(Value.Boolean false), Expr.Variable("nowhere", []))

        Assert.IsFalse(isTrue [] expr)

    [<TestMethod>]
    member _.OrDoesNotEvaluateItsRightSideWhenTheLeftIsTrue() =
        let expr = Expr.Or(Expr.Const(Value.Boolean true), Expr.Variable("nowhere", []))

        Assert.IsTrue(isTrue [] expr)

    [<TestMethod>]
    member _.NotInvertsIt() =
        Assert.IsTrue(isTrue [] (Expr.Not(Expr.Const(Value.Boolean false))))
        Assert.IsFalse(isTrue [] (Expr.Not(Expr.Const(Value.Boolean true))))

    /// A boolean answers a yes-or-no question, and so does the word `true` or `false`,
    /// in any case (decision 0034). A gap is `false`. A predicate that answered
    /// `notes.txt` or `1` has not been asked a question that was answered.
    [<TestMethod>]
    member _.OnlyABooleanOrTheWordForOneIsAnAnswer() =
        let asked = column "done"

        Assert.IsTrue(expectOk (Expr.truth asked (Value.Boolean true)))
        Assert.IsFalse(expectOk (Expr.truth asked (Value.Boolean false)))
        Assert.IsTrue(expectOk (Expr.truth asked (Value.Text "true")))
        Assert.IsTrue(expectOk (Expr.truth asked (Value.Text "TRUE")))
        Assert.IsFalse(expectOk (Expr.truth asked (Value.Text "False")))
        Assert.IsFalse(expectOk (Expr.truth asked Value.None))

        for other in [ Value.Text "notes.txt"; Value.Number 1.0 ] do
            match Expr.truth asked other with
            | Error fault -> Assert.AreEqual<FaultKind>(Invalid, fault.Kind)
            | Ok answer -> Assert.Fail(sprintf "%A was taken as %b." other answer)

    // --------------------------------------------------------------- Display

    /// A query is a value, so it has to read back as something a person could type
    /// again. Phase 4 puts one in the prompt.
    [<TestMethod>]
    member _.AnExpressionReadsBackAsItWasWritten() =
        let expr =
            Expr.And(
                Expr.Compare("eq", column "kind", constant (Value.Text "note")),
                Expr.Not(Expr.Compare("gt", column "size", constant (Value.Number 100.0))))

        Assert.AreEqual<string>(
            "$row.kind eq note and not $row.size gt 100",
            Value.display (Value.Query expr))
