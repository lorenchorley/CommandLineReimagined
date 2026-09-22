namespace CommandLineReimagined.Core.Tests

open System
open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>Replaying a log, seeding once, and starting over.</summary>
/// <remarks>
/// This is what a reload is, from the core's point of view: a second session over the
/// same log. The tests use an in-memory log shared between two sessions, which is
/// exactly the shape the browser has with IndexedDB in place of the dictionary, so the
/// behaviour is pinned here rather than only in a browser where it is slow to check.
/// </remarks>
[<TestClass>]
type PersistenceTests() =

    /// Two sessions over one log, the second standing for a reload.
    let reopen (log: ILog) =
        let mutable counter = 100

        let options =
            { SessionOptions.defaults with
                Clock = (fun () -> DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero))
                NewId =
                    fun () ->
                        counter <- counter + 1
                        sprintf "id-%d" counter }

        let session = Session(log, options, SessionOptions.standardSeed options)
        run (session.Initialize())
        session

    /// The `name` column of a listing, space separated: a listing is a table from
    /// Phase 3, and these tests are about which records came back rather than about
    /// how they are laid out.
    let names (session: Session) (line: string) =
        let response = run (session.Execute line)

        match response.Fault, response.Result with
        | Some fault, _ -> failwith fault.Message
        | None, Some(Value.Table table) ->
            table.Rows
            |> List.map (fun row -> Value.display (Table.cell table "name" row))
            |> String.concat " "
        | None, other -> failwithf "'%s' did not answer a table: %A" line other

    let display (session: Session) (line: string) =
        let response = run (session.Execute line)

        match response.Fault with
        | Some fault -> failwith fault.Message
        | None ->
            match response.Result with
            | Some value -> Value.display value
            | None -> ""

    // ----------------------------------------------------------------- replaying

    /// <summary>A reload brings back what was there.</summary>
    /// <remarks>
    /// The whole of Phase 2, in one test. Everything else about persistence is a
    /// question of where the log is written, not of what replaying it means.
    /// </remarks>
    [<TestMethod>]
    member _.AReopenedSessionHasTheSameFilesystem() =
        let log = InMemoryLog()
        let first = reopen log
        display first "mkdir persisted" |> ignore
        display first "write note.txt hello" |> ignore
        display first "set greeting hi" |> ignore
        display first "cd persisted" |> ignore

        let second = reopen log

        Assert.AreEqual<string>("documents examples persisted projects note.txt readme.txt", names second "ls /")
        Assert.AreEqual<string>("hello", display second "cat /note.txt")
        Assert.AreEqual<string>("hi", display second "echo $greeting")
        Assert.AreEqual<string>("/persisted", second.Location.Folder)

    [<TestMethod>]
    member _.AReopenedSessionReportsWhatItReplayed() =
        let log = InMemoryLog()
        let first = reopen log
        display first "mkdir one" |> ignore
        display first "mkdir two" |> ignore

        let second = reopen log

        // The seed and the two folders.
        Assert.AreEqual<int>(3, second.ReplayedCount)

    [<TestMethod>]
    member _.AFreshSessionReplayedNothing() =
        Assert.AreEqual<int>(0, (reopen (InMemoryLog())).ReplayedCount)

    /// Seeding a second time over a restored log would give the user two of everything.
    [<TestMethod>]
    member _.SeedingDoesNotRunAgainOnANonEmptyLog() =
        let log = InMemoryLog()
        let first = reopen log
        display first "rm readme.txt" |> ignore

        let second = reopen log

        Assert.AreEqual<string>("documents examples projects", names second "ls")

    /// Undo survives a reload, because the compensation chain is in the log rather
    /// than in memory.
    [<TestMethod>]
    member _.UndoWorksAcrossAReload() =
        let log = InMemoryLog()
        let first = reopen log
        display first "mkdir alpha" |> ignore

        let second = reopen log

        Assert.AreEqual<string>("Undone: mkdir alpha", display second "undo")
        Assert.AreEqual<string>("documents examples projects readme.txt", names second "ls")
        Assert.AreEqual<string>("Redone: mkdir alpha", display second "redo")

    /// Content is in the log's blob store, so a reload can still read a file written
    /// before it.
    [<TestMethod>]
    member _.ContentSurvivesAReload() =
        let log = InMemoryLog()
        let first = reopen log
        display first "write a.txt first" |> ignore
        display first "write a.txt second" |> ignore

        let second = reopen log

        Assert.AreEqual<string>("second", display second "cat a.txt")
        display second "undo" |> ignore
        Assert.AreEqual<string>("first", display second "cat a.txt", "The previous version is in the blob store.")

    // --------------------------------------------------------------------- reset

    [<TestMethod>]
    member _.ResetEmptiesTheLogAndSeedsAgain() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore
        harness.Run "write note.txt hello" |> ignore

        Assert.AreEqual<string>("Reset. 9 files restored.", harness.Text "reset")
        Assert.AreEqual<string>("documents examples projects readme.txt", harness.Names "ls")

    [<TestMethod>]
    member _.ResetLeavesNothingToUndo() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore
        harness.Run "reset" |> ignore

        Assert.AreEqual<string>("Nothing to undo.", harness.Text "undo")

    /// It is the one command that cannot be undone, and its description says so, so
    /// `help` warns before rather than after.
    [<TestMethod>]
    member _.ResetSaysItCannotBeUndone() =
        let harness = seeded ()

        let spec = harness.Session.Commands |> List.find (fun c -> c.Name = "reset")

        StringAssert.Contains(spec.Description, "cannot be undone")

    [<TestMethod>]
    member _.ResetClearsVariablesAndReturnsToTheRoot() =
        let harness = seeded ()
        harness.Run "set v 1" |> ignore
        harness.Run "cd documents" |> ignore

        harness.Run "reset" |> ignore

        Assert.AreEqual<string>("/", harness.Location)
        Assert.IsTrue((harness.Variable "v").IsNone)

    [<TestMethod>]
    member _.ResetStartsHistoryAgain() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore

        harness.Run "reset" |> ignore

        Assert.AreEqual<string list>([ "seed" ], harness.History())

    /// After a reset the log holds only the new seed, so reopening it restores that
    /// rather than what was thrown away.
    [<TestMethod>]
    member _.AResetSurvivesAReload() =
        let log = InMemoryLog()
        let first = reopen log
        display first "mkdir alpha" |> ignore
        display first "reset" |> ignore

        let second = reopen log

        Assert.AreEqual<string>("documents examples projects readme.txt", names second "ls")

    /// A session with nothing to seed resets to genuinely nothing, and says so.
    [<TestMethod>]
    member _.ResetWithNoSeedSaysTheFilesystemIsEmpty() =
        let harness = bare ()
        harness.Run "mkdir alpha" |> ignore

        Assert.AreEqual<string>("Reset. The filesystem is empty.", harness.Text "reset")
        Assert.AreEqual<int>(0, List.length (harness.Table "ls").Rows)
