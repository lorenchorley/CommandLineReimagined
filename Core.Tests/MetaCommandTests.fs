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

    /// Pressing redo again, once everything is back, says there is nothing to do
    /// rather than taking it away again.
    [<TestMethod>]
    member _.RedoStopsWhenThereIsNothingLeftToPutBack() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore
        harness.Run "undo" |> ignore
        harness.Run "redo" |> ignore

        Assert.AreEqual<string>("Nothing to redo.", harness.Text "redo")
        Assert.IsTrue(harness.Exists "alpha")

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

        let table = harness.Table "history"

        Assert.AreEqual<int>(3, List.length table.Rows)

        let undone =
            table.Rows
            |> List.filter (fun row -> Table.cell table "undone" row = Value.Boolean true)
            |> List.map (fun row -> Value.display (Table.cell table "source" row))

        CollectionAssert.AreEqual([| "mkdir alpha" |], undone |> Array.ofList)

    /// After a redo the mkdir stands again, and the undo that reversed it has itself
    /// been reversed, so nothing is marked.
    [<TestMethod>]
    member _.AfterARedoNothingIsMarkedUndone() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore
        harness.Run "undo" |> ignore
        harness.Run "redo" |> ignore

        Assert.AreEqual<string list>([], harness.Undone())

        Assert.AreEqual<int>(4, List.length (harness.Table "history").Rows)

    /// An undo and a redo carry the source of the line they reverse, so on their own
    /// they read as the line itself. `compensates` names the transaction each one
    /// reverses: the undo reverses the mkdir, the redo reverses the undo.
    [<TestMethod>]
    member _.HistorySaysWhatEachCompensationReverses() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore
        harness.Run "undo" |> ignore
        harness.Run "redo" |> ignore

        let table = harness.Table "history"

        Assert.AreEqual<string list>([ "seq"; "at"; "source"; "undone"; "compensates" ], Table.names table)

        Assert.AreEqual<Value list>(
            [ Value.None; Value.None; Value.Number 2.0; Value.Number 3.0 ],
            table.Rows |> List.map (Table.cell table "compensates"))

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
        Assert.AreEqual<string>("documents examples guide projects readme.txt", harness.Names "ls")

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
        Assert.AreEqual<string>("documents examples guide projects readme.txt", harness.Names "ls")

    [<TestMethod>]
    member _.HistoryOnAFreshSessionIsEmpty() =
        let harness = bare ()

        Assert.AreEqual<int>(0, List.length (harness.Table "history").Rows)

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

    // ----------------------------------------------------- help <command>

    /// Finding 10: `help where` answers `where`'s parameters, one row each.
    [<TestMethod>]
    member _.HelpForACommandIsATableOfItsParameters() =
        let harness = seeded ()
        let table = harness.Table "help where"

        Assert.AreEqual<string list>([ "name"; "required"; "piped"; "takes"; "description" ], Table.names table)
        Assert.AreEqual<string list>([ "predicate"; "table" ], harness.Column "help where" "name")
        Assert.AreEqual<string list>([ "true"; "false" ], harness.Column "help where" "required")
        Assert.AreEqual<string list>([ "false"; "true" ], harness.Column "help where" "piped")
        Assert.AreEqual<string>("a predicate", List.head (harness.Column "help where" "takes"))

        Assert.AreEqual<string>(
            "An expression over $row, such as $row.kind eq folder",
            List.head (harness.Column "help where" "description"))

    /// The description is written above the table, through the output, so the value
    /// stays a table.
    [<TestMethod>]
    member _.HelpForACommandWritesItsDescriptionAbove() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "Keep the rows a predicate is true for" ], harness.Written "help where")

    [<TestMethod>]
    member _.HelpForACommandCanBePiped() =
        let harness = seeded ()

        Assert.AreEqual<string>("2", harness.Text "help where | count")
        Assert.AreEqual<string>("predicate", harness.Names "help where | where $row.required eq true")

    [<TestMethod>]
    member _.HelpReadsTheCommandsNameInAnyCase() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "predicate"; "table" ], harness.Column "help WHERE" "name")

    /// A command with no parameters is an empty table, not a fault.
    [<TestMethod>]
    member _.HelpForACommandWithoutParametersIsAnEmptyTable() =
        let harness = seeded ()

        Assert.AreEqual<int>(0, List.length (harness.Table "help undo").Rows)
        Assert.AreEqual<string list>([ "Reverse the last line that changed something" ], harness.Written "help undo")

    /// Without a command, `help` is the table of every command it always was.
    [<TestMethod>]
    member _.HelpWithoutACommandListsEveryCommand() =
        let harness = seeded ()
        let table = harness.Table "help"

        Assert.AreEqual<string list>([ "name"; "parameters"; "description" ], Table.names table)
        Assert.AreEqual<int>(List.length harness.Session.Commands, List.length table.Rows)
        Assert.AreEqual<string list>([], harness.Written "help")
        Assert.AreEqual<string list>([ "[command]" ], harness.Column "help | where $row.name eq help" "parameters")

    [<TestMethod>]
    member _.HelpForAnUnknownCommandIsAFault() =
        let harness = seeded ()
        let fault = harness.Fail "help frobnicate"

        Assert.AreEqual<FaultKind>(UnknownCommand, fault.Kind)
        Assert.AreEqual<string>("Unknown command : frobnicate", fault.Message)

    /// <summary>An unknown command given to `help` names the nearest ones.</summary>
    /// <remarks>
    /// Run as a command rather than through the session: `help` is handed
    /// `Nearest.names` by whoever builds it, because `Nearest.fs` is compiled after it.
    /// </remarks>
    [<TestMethod>]
    member _.HelpForAMistypedCommandSaysWhichWasMeant() =
        let specs = (seeded ()).Session.Commands
        let command = Commands.Meta.helpWith Nearest.names (fun () -> specs)

        let invocation =
            { Spec = command.Spec
              Args = Map.ofList [ "command", Value.Text "lss" ]
              Assignments = []
              Input = Value.Empty
              Output = CapturingOutput ignore
              Scope = Scope Map.empty
              Projection = Projection.empty
              Location = Projection.emptyLocation
              Blobs =
                { new IBlobs with
                    member _.Put _ = failwith "help writes nothing"
                    member _.Get _ = async.Return None }
              Cancel = System.Threading.CancellationToken.None }

        match Async.RunSynchronously(command.Run invocation) with
        | Error fault ->
            Assert.AreEqual<string>("Unknown command : lss", fault.Message)
            Assert.AreEqual<string list>([ "Did you mean ls?" ], fault.Notes |> List.map (fun note -> note.Text))
        | Ok _ -> Assert.Fail "Expected a fault."

    /// Finding 9: a mistyped command says which one was meant.
    [<TestMethod>]
    member _.AMistypedCommandSaysWhichWasMeant() =
        let harness = seeded ()
        let fault = harness.Fail "lss"

        Assert.AreEqual<FaultKind>(UnknownCommand, fault.Kind)
        Assert.AreEqual<string>("Unknown command : lss", fault.Message)
        Assert.AreEqual<string list>([ "Did you mean ls?" ], fault.Notes |> List.map (fun note -> note.Text))

    [<TestMethod>]
    member _.SeveralNearCommandsAreAllNamed() =
        let harness = seeded ()

        let fault = harness.Fail "rn"

        Assert.AreEqual<string>("Unknown command : rn", fault.Message)
        Assert.AreEqual<string list>([ "Did you mean in, rm or run?" ], fault.Notes |> List.map (fun note -> note.Text))

    /// A name nothing is near is only named as unknown.
    [<TestMethod>]
    member _.ANameNothingIsNearIsOnlyUnknown() =
        let harness = seeded ()

        Assert.AreEqual<string>("Unknown command : frobnicate", harness.Error "frobnicate")
