namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness

/// <summary>Committing, undoing, redoing, and replaying.</summary>
/// <remarks>
/// Undo is the reason the store exists (decision 0010), and these pin the two things
/// that make it work: the log only grows, so an undo is itself a line that can be
/// undone; and a fresh store fed the same log arrives at the same projection, which is
/// what lets a browser reload restore a session in Phase 2.
/// </remarks>
[<TestClass>]
type StoreTests() =

    let commit (store: Store) source events = run (store.Commit source events) |> expectOk

    let mkdir name parent =
        FileCreated(folder (name + "-id") name parent)

    // ------------------------------------------------------------------ Committing

    [<TestMethod>]
    member _.ACommitAppliesItsEventsAndReturnsTheTransaction() =
        let store = initialised ()
        let transaction = commit store "mkdir alpha" [ mkdir "alpha" "/" ]

        Assert.IsTrue transaction.IsSome
        Assert.AreEqual<int64>(1L, transaction.Value.Seq)
        Assert.AreEqual<string>("mkdir alpha", transaction.Value.Source)
        Assert.AreEqual<int>(1, store.Current.Files.Count)

    /// Decision 0015: a line that changed nothing leaves no trace, so `undo` after an
    /// `ls` reaches past it to the last line that did something.
    [<TestMethod>]
    member _.ALineWithNoEventsCommitsNothing() =
        let store = initialised ()

        Assert.IsTrue((commit store "ls" []).IsNone)
        Assert.AreEqual<int>(0, store.Transactions.Length)

    [<TestMethod>]
    member _.SequenceNumbersCountUpFromOne() =
        let store = initialised ()
        let first = (commit store "mkdir a" [ mkdir "a" "/" ]).Value
        let second = (commit store "mkdir b" [ mkdir "b" "/" ]).Value

        Assert.AreEqual<int64>(1L, first.Seq)
        Assert.AreEqual<int64>(2L, second.Seq)
        Assert.AreEqual<int64>(2L, store.Current.Applied)

    /// The store refuses a name that is already taken whatever the command believed,
    /// because it is the only thing that sees the state the events will land on.
    [<TestMethod>]
    member _.TwoRecordsCannotShareANameInAFolder() =
        let store = initialised ()
        commit store "mkdir alpha" [ mkdir "alpha" "/" ] |> ignore

        let fault =
            expectFault Conflict (run (store.Commit "mkdir alpha" [ FileCreated(folder "other" "alpha" "/") ]))

        Assert.AreEqual<string>("Target file already exists : /alpha", fault.Message)

    /// Names are unique across files and folders together (decision 0016), so a folder
    /// cannot hide behind a file of the same name.
    [<TestMethod>]
    member _.AFolderAndAFileCannotShareAName() =
        let store = initialised ()
        commit store "write notes" [ FileCreated(file "f" "notes" "/") ] |> ignore

        expectFault Conflict (run (store.Commit "mkdir notes" [ FileCreated(folder "d" "notes" "/") ]))
        |> ignore

    [<TestMethod>]
    member _.TheSameNameInDifferentFoldersIsFine() =
        let store = initialised ()

        commit
            store
            "seed"
            [ mkdir "documents" "/"
              FileCreated(file "a" "notes.txt" "/")
              FileCreated(file "b" "notes.txt" "/documents") ]
        |> ignore

        Assert.AreEqual<int>(3, store.Current.Files.Count)

    /// A rejected commit leaves nothing behind, not even the events before the bad one.
    [<TestMethod>]
    member _.ARejectedCommitAppendsNothing() =
        let store = initialised ()
        commit store "mkdir alpha" [ mkdir "alpha" "/" ] |> ignore

        run (store.Commit "two" [ mkdir "beta" "/"; FileCreated(folder "x" "alpha" "/") ])
        |> expectFault Conflict
        |> ignore

        Assert.AreEqual<int>(1, store.Transactions.Length)
        Assert.AreEqual<int>(1, store.Current.Files.Count)

    // ----------------------------------------------------------------------- Undo

    [<TestMethod>]
    member _.UndoRestoresTheProjection() =
        let store = initialised ()
        let before = store.Current
        commit store "mkdir alpha" [ mkdir "alpha" "/" ] |> ignore

        let undone = run (store.Undo()) |> expectOk

        Assert.AreEqual<string>("mkdir alpha", undone.Value.Source)
        Assert.AreEqual<Map<FileId, FileRecord>>(before.Files, store.Current.Files)

    /// The log only grows: undoing appends a compensation rather than removing a line.
    [<TestMethod>]
    member _.UndoAppendsRatherThanRemoves() =
        let store = initialised ()
        commit store "mkdir alpha" [ mkdir "alpha" "/" ] |> ignore
        run (store.Undo()) |> ignore

        Assert.AreEqual<int>(2, store.Transactions.Length)
        Assert.AreEqual<int64 option>(Some 1L, store.Transactions[1].Compensates)

    /// Nothing to undo is not a failure: nothing went wrong.
    [<TestMethod>]
    member _.UndoingAnEmptyLogSucceedsWithNothing() =
        let store = initialised ()

        Assert.IsTrue((run (store.Undo()) |> expectOk).IsNone)

    [<TestMethod>]
    member _.UndoWalksBackOneLineAtATime() =
        let store = initialised ()
        commit store "mkdir a" [ mkdir "a" "/" ] |> ignore
        commit store "mkdir b" [ mkdir "b" "/" ] |> ignore

        Assert.AreEqual<string>("mkdir b", (run (store.Undo()) |> expectOk).Value.Source)
        Assert.AreEqual<string>("mkdir a", (run (store.Undo()) |> expectOk).Value.Source)
        Assert.AreEqual<int>(0, store.Current.Files.Count)

    /// The defect decision 0010 was written for: two `set`s of one variable, undone
    /// twice, must leave it unbound rather than at its first value.
    [<TestMethod>]
    member _.UndoingTwoSetsOfOneVariableUnbindsIt() =
        let store = initialised ()
        commit store "set v 1" [ VariableChanged("v", None, Some(Value.Number 1.0)) ] |> ignore

        commit store "set v 2" [ VariableChanged("v", Some(Value.Number 1.0), Some(Value.Number 2.0)) ]
        |> ignore

        run (store.Undo()) |> ignore
        Assert.AreEqual<Value option>(Some(Value.Number 1.0), Map.tryFind "v" store.Current.Variables)

        run (store.Undo()) |> ignore
        Assert.IsFalse(store.Current.Variables.ContainsKey "v")

    // ----------------------------------------------------------------------- Redo

    [<TestMethod>]
    member _.RedoPutsBackWhatUndoTookAway() =
        let store = initialised ()
        commit store "mkdir alpha" [ mkdir "alpha" "/" ] |> ignore
        run (store.Undo()) |> ignore

        let redone = run (store.Redo()) |> expectOk

        Assert.AreEqual<string>("mkdir alpha", redone.Value.Source, "Redo names the original line, not the undo.")
        Assert.AreEqual<int>(1, store.Current.Files.Count)

    [<TestMethod>]
    member _.RedoingWithNothingUndoneSucceedsWithNothing() =
        let store = initialised ()
        commit store "mkdir alpha" [ mkdir "alpha" "/" ] |> ignore

        Assert.IsTrue((run (store.Redo()) |> expectOk).IsNone)

    /// <summary>Redo stops when there is nothing left that was undone.</summary>
    /// <remarks>
    /// A redo is itself a compensation, so a rule that looked only for "the latest
    /// compensation" found the redo it had just appended and reversed that. Pressing
    /// redo twice then put the change back and took it away again, and would have done
    /// so for as long as anyone kept pressing.
    /// </remarks>
    [<TestMethod>]
    member _.RedoingTwiceDoesNotUndo() =
        let store = initialised ()
        commit store "mkdir alpha" [ mkdir "alpha" "/" ] |> ignore
        run (store.Undo()) |> ignore
        run (store.Redo()) |> ignore

        Assert.IsTrue((run (store.Redo()) |> expectOk).IsNone, "There was nothing left to redo.")
        Assert.AreEqual<int>(1, store.Current.Files.Count, "The folder should still be there.")

    /// Two undos, then two redos, put both back in the order they were taken away.
    [<TestMethod>]
    member _.RedoWalksForwardThroughSeveralUndos() =
        let store = initialised ()
        commit store "mkdir a" [ mkdir "a" "/" ] |> ignore
        commit store "mkdir b" [ mkdir "b" "/" ] |> ignore

        run (store.Undo()) |> ignore
        run (store.Undo()) |> ignore
        Assert.AreEqual<int>(0, store.Current.Files.Count)

        Assert.AreEqual<string>("mkdir a", (run (store.Redo()) |> expectOk).Value.Source)
        Assert.AreEqual<string>("mkdir b", (run (store.Redo()) |> expectOk).Value.Source)
        Assert.AreEqual<int>(2, store.Current.Files.Count)
        Assert.IsTrue((run (store.Redo()) |> expectOk).IsNone)

    /// Undo, redo, undo: the mkdir is gone again. A redo does not exhaust the undo.
    [<TestMethod>]
    member _.UndoRedoUndoLeavesItUndone() =
        let store = initialised ()
        commit store "mkdir alpha" [ mkdir "alpha" "/" ] |> ignore

        run (store.Undo()) |> ignore
        run (store.Redo()) |> ignore
        let undone = run (store.Undo()) |> expectOk

        Assert.AreEqual<string>("mkdir alpha", undone.Value.Source)
        Assert.AreEqual<int>(0, store.Current.Files.Count)
        Assert.AreEqual<int>(4, store.Transactions.Length)

    // -------------------------------------------------------------------- History

    [<TestMethod>]
    member _.HistoryMarksALineThatHasBeenUndone() =
        let store = initialised ()
        commit store "mkdir alpha" [ mkdir "alpha" "/" ] |> ignore
        run (store.Undo()) |> ignore

        let history = store.History()

        Assert.AreEqual<int>(2, history.Length)
        Assert.IsTrue(history[0].Undone, "The mkdir should be marked undone.")
        Assert.IsFalse(history[1].Undone, "The undo is a line in its own right, not an undone one.")

    /// After a redo nothing is marked undone: the mkdir stands again, and the undo
    /// that reversed it has itself been reversed.
    [<TestMethod>]
    member _.AfterARedoNothingIsMarkedUndone() =
        let store = initialised ()
        commit store "mkdir alpha" [ mkdir "alpha" "/" ] |> ignore
        run (store.Undo()) |> ignore
        run (store.Redo()) |> ignore

        let history = store.History()

        Assert.AreEqual<int>(3, history.Length)
        Assert.IsFalse(history |> List.exists (fun entry -> entry.Undone))

    [<TestMethod>]
    member _.HistoryIsOldestFirst() =
        let store = initialised ()
        commit store "mkdir a" [ mkdir "a" "/" ] |> ignore
        commit store "mkdir b" [ mkdir "b" "/" ] |> ignore

        let sources = store.History() |> List.map (fun e -> e.Transaction.Source)

        Assert.AreEqual<string list>([ "mkdir a"; "mkdir b" ], sources)

    // --------------------------------------------------------------------- Replay

    /// The determinism that Phase 2 rests on: a fresh store fed the same log arrives
    /// at the same projection, so a browser reload restores exactly what was there.
    [<TestMethod>]
    member _.ReplayingALogReproducesTheProjection() =
        let log = InMemoryLog()
        let store = Store(log, fixedClock ())
        run (store.Initialize())

        commit store "mkdir documents" [ mkdir "documents" "/" ] |> ignore

        commit
            store
            "write notes.txt hello"
            [ FileCreated(file "n" "notes.txt" "/documents")
              ContentChanged("n", None, Some(Hash.ofText "hello")) ]
        |> ignore

        commit store "set v 1" [ VariableChanged("v", None, Some(Value.Number 1.0)) ] |> ignore

        commit
            store
            "cd documents"
            [ LocationChanged({ Folder = "/"; View = None }, { Folder = "/documents"; View = None }) ]
        |> ignore

        run (store.Undo()) |> ignore

        let replayed = Store(log, fixedClock ())
        run (replayed.Initialize())

        Assert.AreEqual<Map<FileId, FileRecord>>(store.Current.Files, replayed.Current.Files)
        Assert.AreEqual<Map<string, Value>>(store.Current.Variables, replayed.Current.Variables)
        Assert.AreEqual<Location>(store.Current.Location, replayed.Current.Location)
        Assert.AreEqual<int64>(store.Current.Applied, replayed.Current.Applied)

    /// A replayed store can be undone further, because the compensation chain is in
    /// the log rather than in memory.
    [<TestMethod>]
    member _.AReplayedStoreCanStillUndo() =
        let log = InMemoryLog()
        let store = Store(log, fixedClock ())
        run (store.Initialize())
        commit store "mkdir alpha" [ mkdir "alpha" "/" ] |> ignore

        let replayed = Store(log, fixedClock ())
        run (replayed.Initialize())

        let undone = run (replayed.Undo()) |> expectOk

        Assert.AreEqual<string>("mkdir alpha", undone.Value.Source)
        Assert.AreEqual<int>(0, replayed.Current.Files.Count)

    // ---------------------------------------------------------------------- Blobs

    /// Content is addressed by hash, so keeping every version of every file costs one
    /// copy per distinct text. That is what makes a write's before-image free.
    [<TestMethod>]
    member _.IdenticalContentIsStoredOnce() =
        let store = initialised ()

        let first = run (store.PutBlob "hello")
        let second = run (store.PutBlob "hello")
        let other = run (store.PutBlob "goodbye")

        Assert.AreEqual<string>(first, second)
        Assert.AreNotEqual<string>(first, other)
        Assert.AreEqual<string option>(Some "hello", run (store.GetBlob first))

    [<TestMethod>]
    member _.AMissingBlobIsNothingRatherThanAFailure() =
        let store = initialised ()

        Assert.IsTrue((run (store.GetBlob "nosuchhash")).IsNone)

    [<TestMethod>]
    member _.HashesAreLowerCaseHex() =
        let hash = Hash.ofText "hello"

        Assert.AreEqual<int>(64, hash.Length)
        Assert.AreEqual<string>(hash.ToLowerInvariant(), hash)

    /// The `Changed` event is what Phase 4's live views listen to.
    [<TestMethod>]
    member _.CommittingAnnouncesTheNewSequenceNumber() =
        let store = initialised ()
        let seen = ResizeArray<int64>()
        store.Changed.Add seen.Add

        commit store "mkdir a" [ mkdir "a" "/" ] |> ignore
        run (store.Undo()) |> ignore

        Assert.AreEqual<int64 list>([ 1L; 2L ], List.ofSeq seen)
