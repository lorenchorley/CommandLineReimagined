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

    /// A log as the terminal seeded it before decision 0036: no guide, and the old readme.
    let beforeTheGuide () =
        let log = InMemoryLog()
        let options = SessionOptions.defaults

        let files =
            Seed.standardFiles
            |> List.filter (fun file -> file.Folder <> "/guide" && not (file.Folder = "/" && file.Name = "guide"))
            |> List.map (fun file ->
                if file.Name = "readme.txt" then
                    { file with Content = Some "This filesystem lives in the browser tab." }
                else
                    file)

        let session = Session(log, options, Seed.ofFiles options.NewId options.Clock files)
        run (session.Initialize())
        log

    /// The first guide file, which the tests of decision 0040 change or leave alone.
    let firstGuide = Seed.guideFiles.Head
    let guidePath = "guide/" + firstGuide.Name
    let guideText = firstGuide.Content.Value

    /// <summary>A log seeded when the seed said something else (decision 0040).</summary>
    /// <remarks>
    /// Every file with content is seeded with an older text, the way a visitor who came
    /// before decision 0037 has them: a guide and a readme that say `cat` for `read`.
    /// </remarks>
    let seededEarlier () =
        let log = InMemoryLog()
        let options = SessionOptions.defaults

        let files =
            Seed.standardFiles
            |> List.map (fun file ->
                match file.Content with
                | Some text -> { file with Content = Some("An older text.\n" + text.Replace("read ", "cat ")) }
                | None -> file)

        let session = Session(log, options, Seed.ofFiles options.NewId options.Clock files)
        run (session.Initialize())
        log

    let sources (session: Session) =
        session.History() |> List.map (fun entry -> entry.Transaction.Source)

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
        display first "in persisted" |> ignore

        let second = reopen log

        Assert.AreEqual<string>("documents examples guide persisted projects note.txt readme.txt", names second "ls /")
        Assert.AreEqual<string>("hello", display second "read /note.txt")
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

        Assert.AreEqual<string>("documents examples guide projects", names second "ls")

    /// Undo survives a reload, because the compensation chain is in the log rather
    /// than in memory.
    [<TestMethod>]
    member _.UndoWorksAcrossAReload() =
        let log = InMemoryLog()
        let first = reopen log
        display first "mkdir alpha" |> ignore

        let second = reopen log

        Assert.AreEqual<string>("Undone: mkdir alpha", display second "undo")
        Assert.AreEqual<string>("documents examples guide projects readme.txt", names second "ls")
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

        Assert.AreEqual<string>("second", display second "read a.txt")
        display second "undo" |> ignore
        Assert.AreEqual<string>("first", display second "read a.txt", "The previous version is in the blob store.")

    // --------------------------------------------------------------------- reset

    [<TestMethod>]
    member _.ResetEmptiesTheLogAndSeedsAgain() =
        let harness = seeded ()
        harness.Run "mkdir alpha" |> ignore
        harness.Run "write note.txt hello" |> ignore

        Assert.AreEqual<string>("Reset. 17 files restored.", harness.Text "reset")
        Assert.AreEqual<string>("documents examples guide projects readme.txt", harness.Names "ls")

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
        harness.Run "in documents" |> ignore

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

        Assert.AreEqual<string>("documents examples guide projects readme.txt", names second "ls")

    /// A session with nothing to seed resets to genuinely nothing, and says so.
    [<TestMethod>]
    member _.ResetWithNoSeedSaysTheFilesystemIsEmpty() =
        let harness = bare ()
        harness.Run "mkdir alpha" |> ignore

        Assert.AreEqual<string>("Reset. The filesystem is empty.", harness.Text "reset")
        Assert.AreEqual<int>(0, List.length (harness.Table "ls").Rows)

    // ------------------------------------------------ a log begun before the guide

    /// A visitor who came before the guide gets it on their next visit, without `reset`,
    /// and their readme points to it; their own files are untouched.
    [<TestMethod>]
    member _.AnOldLogIsGivenTheGuideOnce() =
        let log = beforeTheGuide ()
        let first = reopen log
        run (first.Execute "write mine.txt kept") |> ignore

        Assert.AreEqual<int>(Seed.guideFiles.Length + 1, run (first.BringUpToDate()))
        Assert.AreEqual<string>("documents examples guide projects mine.txt readme.txt", names first "ls")
        StringAssert.Contains(display first "read readme.txt", "read guide/1-start.txt")
        Assert.AreEqual<string>("kept", display first "read mine.txt")

        // Once only: the next visit finds it there.
        let second = reopen log
        Assert.AreEqual<int>(0, run (second.BringUpToDate()))
        Assert.AreEqual<int>(Seed.guideFiles.Length, (names second "ls guide").Split(' ').Length)

    /// Adding the guide is not a line anyone typed, so `undo` does not take it back.
    [<TestMethod>]
    member _.TheGuideAddedLaterCannotBeUndone() =
        let session = reopen (beforeTheGuide ())
        run (session.BringUpToDate()) |> ignore

        Assert.AreEqual<string>("Nothing to undo.", display session "undo")
        Assert.AreEqual<string>("documents examples guide projects readme.txt", names session "ls")

    /// A guide deleted on purpose stays deleted: it is added only to a log that never had one.
    [<TestMethod>]
    member _.ADeletedGuideIsNotAddedAgain() =
        let log = beforeTheGuide ()
        let first = reopen log
        run (first.BringUpToDate()) |> ignore

        for guide in Seed.guideFiles do
            display first ("rm guide/" + guide.Name) |> ignore

        display first "rm guide" |> ignore

        let second = reopen log
        Assert.AreEqual<int>(0, run (second.BringUpToDate()))
        Assert.AreEqual<string>("documents examples projects readme.txt", names second "ls")

    /// A readme someone has rewritten is theirs, and is left as it is.
    [<TestMethod>]
    member _.ARewrittenReadmeIsKept() =
        let log = beforeTheGuide ()
        let first = reopen log
        run (first.Execute "echo mine | write readme.txt") |> ignore

        let second = reopen log
        run (second.BringUpToDate()) |> ignore
        Assert.AreEqual<string>("mine", display second "read readme.txt")
        Assert.AreEqual<string>("documents examples guide projects readme.txt", names second "ls")

    // ------------------------------------------- seeded files follow the seed (0040)

    /// A returning visitor reads the guide and the readme as the seed says them now.
    [<TestMethod>]
    member _.AnUntouchedGuideFileAndReadmeAreBroughtUpToDate() =
        let session = reopen (seededEarlier ())

        Assert.AreEqual<int>(0, run (session.BringUpToDate()), "No record is created: the files are all there.")
        Assert.AreEqual<string>(guideText, display session ("read " + guidePath))
        Assert.AreEqual<string>(Seed.readme, display session "read readme.txt")

        // Every other seeded file with it, the example programs included.
        for file in Seed.standardFiles do
            match file.Content with
            | Some text -> Assert.AreEqual<string>(text, display session ("read " + file.Folder + "/" + file.Name), file.Name)
            | None -> ()

    /// One transaction, the system's, with the source `seed update`, and nobody's to undo.
    [<TestMethod>]
    member _.TheSeedUpdateIsOneTransactionNobodyCanUndo() =
        let session = reopen (seededEarlier ())
        run (session.BringUpToDate()) |> ignore

        Assert.AreEqual<string list>([ "seed"; "seed update" ], sources session)
        Assert.IsFalse((List.last (session.History())).Transaction.Undoable)
        Assert.AreEqual<string>("Nothing to undo.", display session "undo")
        Assert.AreEqual<string>(guideText, display session ("read " + guidePath))

    [<TestMethod>]
    member _.AnEditedFileIsLeftAlone() =
        let log = seededEarlier ()
        run ((reopen log).Execute("echo mine | write " + guidePath)) |> ignore

        let session = reopen log
        run (session.BringUpToDate()) |> ignore

        Assert.AreEqual<string>("mine", display session ("read " + guidePath))
        Assert.AreEqual<string>(Seed.readme, display session "read readme.txt", "The files nobody changed still follow.")

    /// Written to and put back by `undo` is still written to: the line was the user's.
    [<TestMethod>]
    member _.AFileEditedAndUndoneIsStillTheUsers() =
        let log = seededEarlier ()
        let first = reopen log
        display first ("echo mine | write " + guidePath) |> ignore
        display first "undo" |> ignore

        let session = reopen log
        run (session.BringUpToDate()) |> ignore

        StringAssert.StartsWith(display session ("read " + guidePath), "An older text.")

    /// A renamed file is somewhere the seed does not describe, and is not replaced or
    /// made again at its old name.
    [<TestMethod>]
    member _.ARenamedFileIsLeftAlone() =
        let log = seededEarlier ()
        display (reopen log) ("attr " + guidePath + " name=mine.txt") |> ignore

        let session = reopen log
        run (session.BringUpToDate()) |> ignore

        StringAssert.StartsWith(display session "read guide/mine.txt", "An older text.")
        Assert.IsFalse((names session "ls guide").Split(' ') |> Array.contains firstGuide.Name)

    /// Renaming the folder and back moves every file in it, and a moved file is the
    /// user's even when it is back at the path the seed describes.
    [<TestMethod>]
    member _.AFileMovedAndMovedBackIsLeftAlone() =
        let log = seededEarlier ()
        let first = reopen log
        display first "attr guide name=manual" |> ignore
        display first "attr manual name=guide" |> ignore

        let session = reopen log
        run (session.BringUpToDate()) |> ignore

        StringAssert.StartsWith(display session ("read " + guidePath), "An older text.")

    [<TestMethod>]
    member _.ATaggedFileIsLeftAlone() =
        let log = seededEarlier ()
        display (reopen log) ("attr " + guidePath + " level=first") |> ignore

        let session = reopen log
        run (session.BringUpToDate()) |> ignore

        StringAssert.StartsWith(display session ("read " + guidePath), "An older text.")

    [<TestMethod>]
    member _.ADeletedFileIsNotBroughtBack() =
        let log = seededEarlier ()
        display (reopen log) ("rm " + guidePath) |> ignore

        let session = reopen log
        run (session.BringUpToDate()) |> ignore

        Assert.IsFalse((names session "ls guide").Split(' ') |> Array.contains firstGuide.Name)

    /// A file of the user's own at a seeded path, made after they deleted the seed's,
    /// is theirs.
    [<TestMethod>]
    member _.AFileMadeAgainAtASeededPathIsTheUsers() =
        let log = seededEarlier ()
        let first = reopen log
        display first "rm readme.txt" |> ignore
        display first "echo mine | write readme.txt" |> ignore

        let session = reopen log
        run (session.BringUpToDate()) |> ignore

        Assert.AreEqual<string>("mine", display session "read readme.txt")

    /// Once up to date, a second load finds nothing to change and commits nothing.
    [<TestMethod>]
    member _.ASecondLoadChangesNothing() =
        let log = seededEarlier ()
        run ((reopen log).BringUpToDate()) |> ignore
        let count = log.Count

        let second = reopen log
        Assert.AreEqual<int>(0, run (second.BringUpToDate()))
        Assert.AreEqual<int>(count, log.Count)
        Assert.AreEqual<string list>([ "seed"; "seed update" ], sources second)

    /// A fresh log already has the seed's current text, so there is nothing to update.
    [<TestMethod>]
    member _.AFreshLogIsAlreadyUpToDate() =
        let session = reopen (InMemoryLog())
        run (session.BringUpToDate()) |> ignore

        Assert.AreEqual<string list>([ "seed" ], sources session)

    /// The user's own lines stay in the log beneath it, and `undo` still reaches them.
    [<TestMethod>]
    member _.TheUsersLinesAreUntouchedByTheUpdate() =
        let log = seededEarlier ()
        display (reopen log) "mkdir mine" |> ignore

        let session = reopen log
        run (session.BringUpToDate()) |> ignore

        Assert.AreEqual<string list>([ "seed"; "mkdir mine"; "seed update" ], sources session)
        Assert.AreEqual<string>("Undone: mkdir mine", display session "undo")
        Assert.AreEqual<string>(guideText, display session ("read " + guidePath))
