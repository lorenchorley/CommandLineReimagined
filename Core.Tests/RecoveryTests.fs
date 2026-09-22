namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>Errors in the language: `else`, `try`, `??` and nested pipelines (Phase 5).</summary>
/// <remarks>
/// Decision 0014 and decision 0023. What these pin is less the happy paths than what
/// stays true around a failure: a failed branch or a failed `try` leaves no trace, the
/// branch that produced the value is the one whose events commit, and a fault held as a
/// value can be asked what it was.
/// </remarks>
[<TestClass>]
type RecoveryTests() =

    let faultOf (value: Value) =
        match value with
        | Value.Fault fault -> fault
        | other -> raise (AssertFailedException(sprintf "Expected a fault value, got %s." (Value.kind other)))

    // ------------------------------------------------------------------- else

    [<TestMethod>]
    member _.ElseIsNotRunWhenTheLeftSucceeds() =
        let harness = seeded ()

        Assert.AreEqual<string>("This filesystem lives in the browser tab.", harness.Text "cat readme.txt else echo none")

    [<TestMethod>]
    member _.ElseRunsWhenTheLeftFails() =
        let harness = seeded ()

        Assert.AreEqual<string>("none", harness.Text "cat missing.txt else echo \"none\"")

    /// The fault is the right side's pipe input, so `else echo` prints it.
    [<TestMethod>]
    member _.TheFaultIsPipedIntoTheRightSide() =
        let harness = seeded ()

        Assert.AreEqual<string>("File does not exist : /missing.txt", harness.Text "cat missing.txt else echo")

    [<TestMethod>]
    member _.ElseCanBindTheFault() =
        let harness = seeded ()
        harness.Run "cat missing.txt else set problem" |> ignore

        match harness.Variable "problem" with
        | Some(Value.Fault fault) -> Assert.AreEqual(NotFound, fault.Kind)
        | other -> Assert.Fail(sprintf "Expected $problem to hold a fault, got %A." other)

    /// `a | b else c | d` is `(a | b) else (c | d)`: the right pipeline is whole.
    [<TestMethod>]
    member _.ElseBindsLooserThanThePipe() =
        let harness = seeded ()
        harness.Run "cat missing.txt else echo \"starting fresh\" | write today.txt" |> ignore

        Assert.AreEqual<string>("starting fresh", harness.Content "/today.txt")

    /// Decision 0015 still holds across `else`: the failed left side commits nothing.
    [<TestMethod>]
    member _.AFailedLeftBranchCommitsNothing() =
        let harness = seeded ()
        harness.Run "mkdir today | cd nowhere else echo \"rolled back\"" |> ignore

        Assert.IsFalse(harness.Exists "/today", "The mkdir on the failed side of else was committed.")

    [<TestMethod>]
    member _.TheRightBranchsEventsCommit() =
        let harness = seeded ()
        harness.Run "mkdir left | cd nowhere else mkdir right" |> ignore

        Assert.IsFalse(harness.Exists "/left")
        Assert.IsTrue(harness.Exists "/right")
        Assert.AreEqual<string>("mkdir left | cd nowhere else mkdir right", List.last (harness.History()))

    /// Undo takes back the whole line, which is the right branch's work and nothing else.
    [<TestMethod>]
    member _.UndoTakesBackTheBranchThatRan() =
        let harness = seeded ()
        harness.Run "cat missing.txt else mkdir fresh" |> ignore
        harness.Run "undo" |> ignore

        Assert.IsFalse(harness.Exists "/fresh")

    [<TestMethod>]
    member _.ElseChainsLeftToRight() =
        let harness = seeded ()

        Assert.AreEqual<string>("third", harness.Text "cat a.txt else cat b.txt else echo third")

    /// The second branch's fault is what the third sees, not the first's.
    [<TestMethod>]
    member _.EachBranchSeesTheFaultBeforeIt() =
        let harness = seeded ()

        Assert.AreEqual<string>("File does not exist : /b.txt", harness.Text "cat a.txt else cat b.txt else echo")

    [<TestMethod>]
    member _.WhenEveryBranchFailsTheLastFaultIsTheLines() =
        let harness = seeded ()

        Assert.AreEqual<string>("File does not exist : /b.txt", harness.Error "cat a.txt else cat b.txt")

    // -------------------------------------------------------------------- try

    [<TestMethod>]
    member _.TryTurnsAFailureIntoAValue() =
        let harness = seeded ()
        let fault = faultOf (harness.Run "try cat nowhere.txt")

        Assert.AreEqual(NotFound, fault.Kind)
        Assert.AreEqual<string>("File does not exist : /nowhere.txt", fault.Message)

    [<TestMethod>]
    member _.TryOnASuccessIsTheValue() =
        let harness = seeded ()

        Assert.AreEqual<string>("hello", harness.Text "try echo hello")

    [<TestMethod>]
    member _.TheNextStageReceivesTheFault() =
        let harness = seeded ()
        harness.Run "try cat nowhere.txt | set problem" |> ignore

        Assert.AreEqual<string>("NotFound", harness.Text "echo $problem.kind")
        Assert.AreEqual<string>("File does not exist : /nowhere.txt", harness.Text "echo $problem.message")
        Assert.AreEqual<string>("/nowhere.txt", harness.Text "echo $problem.path")

    /// The fault remembers which stage it came from, counted from one.
    [<TestMethod>]
    member _.ACaughtFaultCarriesItsStage() =
        let harness = seeded ()
        harness.Run "echo x | try cat nowhere.txt | set problem" |> ignore

        Assert.AreEqual<string>("2", harness.Text "echo $problem.stage")

    /// A pipeline in parentheses is one stage of the line around it, whether it stands
    /// as the stage or as an argument, so a fault from inside names the outer stage. As
    /// a stage it used to leak its own inner number.
    [<TestMethod>]
    member _.AParenthesisedStageReportsTheOuterStage() =
        let harness = seeded ()
        harness.Run "try (echo a | cat nowhere) | set standing" |> ignore
        harness.Run "try echo (echo a | cat nowhere) | set argument" |> ignore

        Assert.AreEqual<string>("1", harness.Text "echo $argument.stage")
        Assert.AreEqual<string>("1", harness.Text "echo $standing.stage")

    [<TestMethod>]
    member _.AFailedTryStageLeavesNoEvents() =
        let harness = seeded ()
        // `try` covers a nested pipeline stage whose first command succeeds and second
        // fails: the mkdir must go with the failure.
        harness.Run "try (mkdir inside | cd nowhere) | set problem" |> ignore

        Assert.IsFalse(harness.Exists "/inside", "The events of the failed try stage were committed.")
        Assert.IsTrue((harness.Variable "problem").IsSome, "The stage after try should still have run.")

    [<TestMethod>]
    member _.TryOnlyCoversItsOwnStage() =
        let harness = seeded ()

        Assert.AreEqual<string>("File does not exist : /nowhere.txt", harness.Error "try echo x | cat nowhere.txt")

    /// Stop is not a failure a line recovers from, or the Stop button is a suggestion.
    [<TestMethod>]
    member _.TryDoesNotCatchAStop() =
        let harness = seeded ()
        let cancelled = new System.Threading.CancellationTokenSource()
        cancelled.Cancel()

        let response =
            Async.RunSynchronously(harness.Session.Execute("try progress 3 | set p else echo caught", 0, cancelled.Token))

        Assert.AreEqual<FaultKind option>(Some Cancelled, response.Fault |> Option.map (fun f -> f.Kind))
        Assert.AreEqual<Value option>(None, harness.Variable "p")

    // --------------------------------------------------------------------- ??

    [<TestMethod>]
    member _.TheDefaultReplacesNothing() =
        let harness = seeded ()

        Assert.AreEqual<string>("no views yet", harness.Text "first (ls | where $row.kind eq view) ?? \"no views yet\"")

    [<TestMethod>]
    member _.TheDefaultIsNotUsedWhenThereIsAValue() =
        let harness = seeded ()

        Assert.AreEqual<string>("documents", Value.display (Expr.readMember "name" (harness.Run "first (ls) ?? none")))

    [<TestMethod>]
    member _.TheDefaultFlowsOnDownThePipe() =
        let harness = seeded ()
        harness.Run "first (ls | where $row.kind eq view) ?? \"no views yet\" | set latest" |> ignore

        Assert.AreEqual<string>("no views yet", harness.Text "echo $latest")

    /// The default is evaluated only when it is needed, so one that would fail is harmless.
    [<TestMethod>]
    member _.TheDefaultIsLazy() =
        let harness = seeded ()

        Assert.AreEqual<string>("hello", harness.Text "echo hello ?? (cat nowhere.txt)")
        Assert.AreEqual<string>("File does not exist : /nowhere.txt", harness.Error "first (ls | where $row.kind eq view) ?? (cat nowhere.txt)")

    /// A fault is an answer, so `try x ?? y` keeps it.
    [<TestMethod>]
    member _.AFaultIsNotReplacedByTheDefault() =
        let harness = seeded ()

        Assert.AreEqual(NotFound, (faultOf (harness.Run "try cat nowhere.txt ?? fine")).Kind)

    // ---------------------------------------------------- nested pipelines

    [<TestMethod>]
    member _.ANestedPipelineIsAnArgument() =
        let harness = seeded ()

        Assert.AreEqual<string>("4", harness.Text "echo (ls | count)")

    [<TestMethod>]
    member _.ANestedPipelineCanBeComparedAgainst() =
        let harness = seeded ()

        Assert.AreEqual<string>("readme.txt", harness.Names "ls | where $row.size gt (ls | count)")

    [<TestMethod>]
    member _.ANestedPipelineGetsNoPipeInput() =
        let harness = seeded ()

        Assert.AreEqual<string>("4", harness.Text "echo ignored | echo (ls | count)")

    [<TestMethod>]
    member _.ANestedStageReceivesThePipe() =
        let harness = seeded ()

        Assert.AreEqual<string>("3", harness.Text "ls | (where $row.kind eq folder | count)")

    /// Its events join the line's transaction: a later stage sees them, and undo takes
    /// them back with the rest of the line.
    [<TestMethod>]
    member _.ANestedPipelinesEventsJoinTheLine() =
        let harness = seeded ()
        harness.Run "echo (mkdir made) | set name" |> ignore

        Assert.IsTrue(harness.Exists "/made")
        Assert.AreEqual(1, harness.History() |> List.filter (fun s -> s.StartsWith "echo (mkdir") |> List.length)
        harness.Run "undo" |> ignore
        Assert.IsFalse(harness.Exists "/made")
        Assert.AreEqual<Value option>(None, harness.Variable "name")

    [<TestMethod>]
    member _.AFailingNestedPipelineFailsTheLine() =
        let harness = seeded ()
        let fault = harness.Fail "echo x | echo (cat nowhere.txt)"

        Assert.AreEqual(NotFound, fault.Kind)
        Assert.AreEqual<int option>(Some 2, fault.Stage)

    /// Decision 0023: the spaced form is a nested pipeline, the adjacent one a call.
    [<TestMethod>]
    member _.FirstSpaceParenthesisTakesAPipelinesResult() =
        let harness = seeded ()

        Assert.AreEqual<string>("readme.txt", Value.display (Expr.readMember "name" (harness.Run "first (ls | sort name desc)")))

    // --------------------------------------------------------------- is-fault

    [<TestMethod>]
    member _.IsFaultAnswersWhetherAValueIsOne() =
        let harness = seeded ()
        harness.Run "try cat nowhere.txt | set problem" |> ignore

        Assert.AreEqual<Value>(Value.Boolean true, harness.Run "is-fault $problem")
        Assert.AreEqual<Value>(Value.Boolean true, harness.Run "try cat nowhere.txt | is-fault")
        Assert.AreEqual<Value>(Value.Boolean false, harness.Run "echo fine | is-fault")
        Assert.AreEqual<Value>(Value.Boolean false, harness.Run "is-fault")

    // ------------------------------------------------------------ refresh

    /// A live refresh reaches into every part of the line before deciding it only reads.
    [<TestMethod>]
    member _.RefreshRefusesALineThatWritesAnywhere() =
        let harness = seeded ()

        for line in [ "ls else mkdir x"; "echo (mkdir x)"; "ls ?? (mkdir x)"; "ls | (mkdir x)" ] do
            let response = harness.Refresh line
            Assert.IsTrue(response.Fault.IsSome, sprintf "'%s' should have been refused." line)

        Assert.IsTrue((harness.Refresh "ls | where $row.size gt (ls | count) else echo none").Fault.IsNone)
