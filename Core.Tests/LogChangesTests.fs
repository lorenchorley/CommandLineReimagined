namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>What a line says it did to the log.</summary>
/// <remarks>
/// A host uses this to make `undo` take a line off the screen and `redo` put it back,
/// so what is checked is that every number names the line a person ran, never the
/// compensation the store appended to reverse it.
/// </remarks>
[<TestClass>]
type LogChangesTests() =

    let committed (response: Response) =
        match response.Changes.Committed with
        | [ seq ] -> seq
        | other -> failwithf "Expected one committed line, got %A" other

    [<TestMethod>]
    member _.ALineThatChangesSomethingNamesItsOwnTransaction() =
        let harness = seeded ()
        let response = harness.Respond "mkdir alpha"

        let seq = committed response
        Assert.AreEqual<int64>(seq, (harness.Session.History() |> List.last).Transaction.Seq)
        Assert.IsTrue(response.Changes.Undone.IsEmpty)
        Assert.IsTrue(response.Changes.Redone.IsEmpty)
        Assert.IsFalse(response.Changes.Reset)

    [<TestMethod>]
    member _.AReadOnlyLineChangesNothing() =
        let harness = seeded ()

        Assert.AreEqual<LogChanges>(LogChanges.none, (harness.Respond "ls").Changes)

    [<TestMethod>]
    member _.UndoNamesTheLineRatherThanItsCompensation() =
        let harness = seeded ()
        let alpha = committed (harness.Respond "mkdir alpha")

        let undo = harness.Respond "undo"

        Assert.IsTrue(undo.Changes.Committed.IsEmpty)
        Assert.AreEqual<int64 list>([ alpha ], undo.Changes.Undone)
        Assert.IsTrue(undo.Changes.Redone.IsEmpty)

    [<TestMethod>]
    member _.RedoNamesTheLineRatherThanTheUndo() =
        let harness = seeded ()
        let alpha = committed (harness.Respond "mkdir alpha")
        harness.Run "undo" |> ignore

        let redo = harness.Respond "redo"

        Assert.IsTrue(redo.Changes.Committed.IsEmpty)
        Assert.IsTrue(redo.Changes.Undone.IsEmpty)
        Assert.AreEqual<int64 list>([ alpha ], redo.Changes.Redone)

    /// Undoing a line that was redone names the same line again, so a host keeping one
    /// number per entry can hide and show the same entry as often as it is asked.
    [<TestMethod>]
    member _.UndoAfterRedoNamesTheSameLine() =
        let harness = seeded ()
        let alpha = committed (harness.Respond "mkdir alpha")
        harness.Run "undo" |> ignore
        harness.Run "redo" |> ignore

        Assert.AreEqual<int64 list>([ alpha ], (harness.Respond "undo").Changes.Undone)
        Assert.AreEqual<int64 list>([ alpha ], (harness.Respond "redo").Changes.Redone)

    [<TestMethod>]
    member _.UndoingInTurnNamesEachLineInTurn() =
        let harness = seeded ()
        let alpha = committed (harness.Respond "mkdir alpha")
        let beta = committed (harness.Respond "mkdir beta")

        Assert.AreEqual<int64 list>([ beta ], (harness.Respond "undo").Changes.Undone)
        Assert.AreEqual<int64 list>([ alpha ], (harness.Respond "undo").Changes.Undone)
        Assert.AreEqual<int64 list>([ alpha ], (harness.Respond "redo").Changes.Redone)
        Assert.AreEqual<int64 list>([ beta ], (harness.Respond "redo").Changes.Redone)

    [<TestMethod>]
    member _.NothingToUndoChangesNothing() =
        let harness = seeded ()

        Assert.AreEqual<LogChanges>(LogChanges.none, (harness.Respond "undo").Changes)
        Assert.AreEqual<LogChanges>(LogChanges.none, (harness.Respond "redo").Changes)

    /// A script's lines each commit their own transaction (decision 0020), and
    /// journal.clr ends by undoing one of its own, so its run names both.
    [<TestMethod>]
    member _.RunNamesEveryLineOfItsScript() =
        let harness = seeded ()
        let changes = (harness.Respond "run examples/journal.clr").Changes

        Assert.IsTrue(List.length changes.Committed > 1)

        match changes.Undone with
        | [ seq ] -> Assert.IsTrue(List.contains seq changes.Committed)
        | other -> Assert.Fail(sprintf "Expected one undone line, got %A" other)

    /// After `reset` the numbers start again, so the ones a host remembers from before
    /// it would name whichever new line happened to reuse them.
    [<TestMethod>]
    member _.ResetSaysTheNumbersStartedAgain() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore

        let reset = harness.Respond "reset"

        Assert.IsTrue(reset.Changes.Reset)
        // The seed it appends again is nobody's line.
        Assert.IsTrue(reset.Changes.Committed.IsEmpty)
        Assert.IsFalse((harness.Respond "mkdir beta").Changes.Reset)

    [<TestMethod>]
    member _.AFailedLineChangesNothing() =
        let harness = seeded ()

        Assert.AreEqual<LogChanges>(LogChanges.none, (harness.Respond "mkdir a | cd nowhere").Changes)

    [<TestMethod>]
    member _.ARefreshChangesNothing() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore

        Assert.AreEqual<LogChanges>(LogChanges.none, (harness.Refresh "ls").Changes)
