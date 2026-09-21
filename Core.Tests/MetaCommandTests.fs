namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>undo, redo and history as commands.</summary>
/// <remarks>
/// They were a key press and a bridge method before, which meant the browser and the
/// desktop each had their own half of the feature and neither could pipe it anywhere.
/// They are commands now, and the only ones given a `StoreAccess`.
/// </remarks>
[<TestClass>]
type MetaCommandTests() =

    [<TestMethod>]
    member _.UndoNamesWhatItReversed() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore

        Assert.AreEqual<string>("Undone: mkdir alpha", harness.Text "undo")

    [<TestMethod>]
    member _.RedoNamesTheOriginalLine() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore
        harness.Run "undo" |> ignore

        Assert.AreEqual<string>("Redone: mkdir alpha", harness.Text "redo")
        Assert.IsTrue(harness.Exists "alpha")

    /// <summary>Nothing to undo is a plain result, not a fault.</summary>
    /// <remarks>
    /// Nothing went wrong: the user asked a question and the answer is "there is
    /// nothing". A fault would paint the line red for a state that is perfectly
    /// ordinary at the start of a session.
    /// </remarks>
    [<TestMethod>]
    member _.NothingToUndoIsNotAFailure() =
        let harness = bare ()
        let response = harness.Respond "undo"

        Assert.IsTrue(response.Fault.IsNone)
        Assert.AreEqual<Value option>(Some(Value.Text "Nothing to undo."), response.Result)

    [<TestMethod>]
    member _.NothingToRedoIsNotAFailure() =
        let harness = bare ()
        let response = harness.Respond "redo"

        Assert.IsTrue(response.Fault.IsNone)
        Assert.AreEqual<Value option>(Some(Value.Text "Nothing to redo."), response.Result)

    /// <summary>A read-only line leaves no transaction (decision 0015).</summary>
    /// <remarks>
    /// So `undo` after an `ls` undoes the line before the `ls`. Under the old history
    /// every executed command was on the stack, including ones that changed nothing,
    /// and undoing an `ls` looked exactly like undo being broken.
    /// </remarks>
    [<TestMethod>]
    member _.UndoReachesPastAReadOnlyLine() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore
        harness.Run "ls" |> ignore
        harness.Run "pwd" |> ignore

        Assert.AreEqual<string>("Undone: mkdir alpha", harness.Text "undo")
        Assert.IsFalse(harness.Exists "alpha")

    [<TestMethod>]
    member _.MetaCommandsDoNotAppearInHistory() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore
        harness.Run "history" |> ignore
        harness.Run "vars" |> ignore

        Assert.AreEqual<string list>([ "seed"; "mkdir alpha" ], harness.History())

    /// An undo is a line in its own right, so it appears; what it reversed is marked.
    [<TestMethod>]
    member _.HistoryShowsTheUndoneLine() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore
        harness.Run "undo" |> ignore

        Assert.AreEqual<string list>([ "mkdir alpha" ], harness.Undone())

        match harness.Run "history" with
        | Value.List lines ->
            let text = lines |> List.map Value.display
            Assert.AreEqual<int>(3, text.Length)
            Assert.IsTrue(text |> List.exists (fun line -> line.Contains "mkdir alpha" && line.Contains "(undone)"))
        | other -> Assert.Fail(sprintf "Expected a list, got %A" other)

    /// After a redo the mkdir stands again, and the undo that reversed it has itself
    /// been reversed, so nothing is marked.
    [<TestMethod>]
    member _.AfterARedoNothingIsMarkedUndone() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore
        harness.Run "undo" |> ignore
        harness.Run "redo" |> ignore

        Assert.AreEqual<string list>([], harness.Undone())

        match harness.Run "history" with
        | Value.List lines -> Assert.AreEqual<int>(4, lines.Length)
        | other -> Assert.Fail(sprintf "Expected a list, got %A" other)

    /// <summary>The seed is recorded but is nobody's to undo (decision 0018).</summary>
    /// <remarks>
    /// It is a transaction like any other, so a replay reproduces it and `history`
    /// lists it. Undoing it is what a curious new user's first keystroke would
    /// otherwise do, and it would empty the very files that are there to be looked at.
    /// </remarks>
    [<TestMethod>]
    member _.TheSeedCannotBeUndone() =
        let harness = seeded ()

        Assert.AreEqual<string>("Nothing to undo.", harness.Text "undo")
        Assert.AreEqual<string>("documents projects readme.txt", harness.Text "ls")

    [<TestMethod>]
    member _.TheSeedIsStillInHistory() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "seed" ], harness.History())

    /// Undo walks back to the last line someone typed and stops there, rather than
    /// carrying on into what the session started with.
    [<TestMethod>]
    member _.UndoStopsAtTheSeed() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore

        Assert.AreEqual<string>("Undone: mkdir alpha", harness.Text "undo")
        Assert.AreEqual<string>("Nothing to undo.", harness.Text "undo")
        Assert.AreEqual<string>("documents projects readme.txt", harness.Text "ls")

    [<TestMethod>]
    member _.HistoryOnAFreshSessionSaysSo() =
        let harness = bare ()

        Assert.AreEqual<Value>(Value.Text "Nothing has happened yet.", harness.Run "history")

    /// Every line's own text is what `history` and `Undone:` name, so a user recognises
    /// what they are about to take back.
    [<TestMethod>]
    member _.HistoryNamesEachLineAsItWasWritten() =
        let harness = seeded ()
        harness.Run "mkdir  alpha" |> ignore

        Assert.AreEqual<string>("Undone: mkdir  alpha", harness.Text "undo")

    [<TestMethod>]
    member _.ExitCallsTheHostsShutdown() =
        let mutable exited = false
        let log = InMemoryLog()

        let session =
            Session(log, { SessionOptions.defaults with Exit = fun () -> exited <- true }, Seed.none)

        Async.RunSynchronously(session.Initialize())
        Async.RunSynchronously(session.Execute "exit") |> ignore

        Assert.IsTrue exited

    /// The commands list is what `help` and completion show, and the internal
    /// `UnknownCommand` is not one of them.
    [<TestMethod>]
    member _.TheCommandListExcludesTheUnknownCommandReporter() =
        let harness = bare ()
        let names = harness.Session.Commands |> List.map (fun spec -> spec.Name)

        assertDoesNotContain "UnknownCommand" names
        assertContains "undo" names
        assertContains "attr" names
