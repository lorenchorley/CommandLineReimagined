namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>set and vars.</summary>
/// <remarks>
/// The port of the variable half of `FileAndVariableCommandTests.cs`. The last two
/// cases are the defect decision 0010 was written for, which the old design could not
/// fix without removing per-command undo state entirely.
/// </remarks>
[<TestClass>]
type VariableCommandTests() =

    [<TestMethod>]
    member _.SetBindsAVariableAndReturnsTheValue() =
        let harness = seeded ()

        Assert.AreEqual<string>("hello", harness.Text "set greeting hello")
        Assert.AreEqual<Value option>(Some(Value.Text "hello"), harness.Variable "greeting")

    [<TestMethod>]
    member _.SetBindsWhatWasPiped() =
        let harness = seeded ()
        harness.Run "ls | set files" |> ignore

        match harness.Variable "files" with
        | Some(Value.List _) -> ()
        | other -> Assert.Fail(sprintf "Expected a list, got %A" other)

    [<TestMethod>]
    member _.SetVariableCanBeReadBack() =
        let harness = seeded ()
        harness.Run "set n 42" |> ignore

        Assert.AreEqual<Value>(Value.Number 42.0, harness.Run "echo $n")

    [<TestMethod>]
    member _.SetBindsATagWrittenAsAnArgument() =
        let harness = seeded ()
        harness.Run "set shape <square side=2/>" |> ignore

        match harness.Variable "shape" with
        | Some(Value.Object tag) ->
            Assert.AreEqual<string>("square", tag.TypeName)
            Assert.AreEqual<Value>(Value.Number 2.0, tag.Attributes["side"])
        | other -> Assert.Fail(sprintf "Expected an object, got %A" other)

    [<TestMethod>]
    member _.SetWithoutAValueIsReported() =
        let harness = seeded ()

        StringAssert.Contains(harness.Error "set lonely", "needs an argument for 'value'")

    /// `set $x 1` reads `$x` rather than naming it, the same as everywhere else in the
    /// grammar, so the mistake is reported instead of guessed at.
    [<TestMethod>]
    member _.SetWithAVariableReferenceAsTheNameIsReported() =
        let harness = seeded ()

        StringAssert.Contains(harness.Error "set $x 1", "Unknown variable")

    [<TestMethod>]
    member _.SetRefusesANameThatIsNotOne() =
        let harness = seeded ()

        StringAssert.Contains(harness.Error "set \"not a name\" 1", "is not a valid variable name")

    [<TestMethod>]
    member _.UndoOfSetRemovesANewVariable() =
        let harness = seeded ()
        harness.Run "set temp 1" |> ignore

        harness.Run "undo" |> ignore

        Assert.IsTrue((harness.Variable "temp").IsNone)

    [<TestMethod>]
    member _.UndoOfSetRestoresThePreviousBinding() =
        let harness = seeded ()
        harness.Run "set v first" |> ignore
        harness.Run "set v second" |> ignore

        harness.Run "undo" |> ignore

        Assert.AreEqual<Value option>(Some(Value.Text "first"), harness.Variable "v")

    /// <summary>The defect that decision 0010 was written for.</summary>
    /// <remarks>
    /// Two `set`s of one variable, undone twice, used to leave `$v` at 1 rather than
    /// unbound: the command instance holding the undo state was shared, so the second
    /// undo replayed the first invocation's saved value. There is no per-command undo
    /// state any more, so there is nothing left to get wrong.
    /// </remarks>
    [<TestMethod>]
    member _.UndoingTwoSetsOfOneVariableUnbindsIt() =
        let harness = seeded ()
        harness.Run "set v 1" |> ignore
        harness.Run "set v 2" |> ignore

        harness.Run "undo" |> ignore
        harness.Run "undo" |> ignore

        Assert.IsTrue((harness.Variable "v").IsNone, "$v should be unbound, not back at 1.")
        StringAssert.Contains(harness.Error "echo $v", "Unknown variable")

    [<TestMethod>]
    member _.VarsListsEveryVariableOnItsOwnLine() =
        let harness = seeded ()
        harness.Run "set a 1" |> ignore
        harness.Run "set b two" |> ignore

        let written = harness.Written "vars"

        assertContains "$a = 1" written
        assertContains "$b = two" written

    [<TestMethod>]
    member _.VarsReturnsTheValues() =
        let harness = seeded ()
        harness.Run "set a 1" |> ignore
        harness.Run "set b two" |> ignore

        match harness.Run "vars" with
        | Value.List items -> Assert.AreEqual<int>(2, items.Length)
        | other -> Assert.Fail(sprintf "Expected a list, got %A" other)

    [<TestMethod>]
    member _.VarsWithNothingBoundSaysSo() =
        let harness = seeded ()

        Assert.AreEqual<Value>(Value.Empty, harness.Run "vars")
        Assert.IsTrue(harness.Written "vars" |> List.exists (fun line -> line.Contains "No variables"))

    /// A variable survives a reload, because the binding is in the log rather than in
    /// a scope object. This is what Phase 2 turns into persistence.
    [<TestMethod>]
    member _.VariablesAreProjectedFromTheLog() =
        let harness = seeded ()
        harness.Run "set kept yes" |> ignore

        Assert.AreEqual<Value option>(Some(Value.Text "yes"), Map.tryFind "kept" harness.Projection.Variables)
