namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness

/// <summary>Folding events, and undoing them.</summary>
/// <remarks>
/// The property that matters is that inverting is exact: applying an event and then
/// its inverse leaves the projection it started from, for every event shape rather
/// than for the ones someone remembered to write a case for. Undo is built on that
/// identity, so it is checked as a property over `everyEventShape` and not only by
/// example.
/// </remarks>
[<TestClass>]
type ProjectionTests() =

    [<TestMethod>]
    member _.AnEmptyProjectionIsAtTheRootWithNothingInIt() =
        Assert.AreEqual<string>("/", Projection.empty.Location.Folder)
        Assert.AreEqual<int>(0, Projection.empty.Files.Count)
        Assert.AreEqual<int>(0, Projection.empty.Variables.Count)

    [<TestMethod>]
    member _.CreatingAFileAddsIt() =
        let after = Projection.apply Projection.empty (FileCreated(file "a" "notes.txt" "/"))

        Assert.AreEqual<int>(1, after.Files.Count)
        Assert.AreEqual<string>("notes.txt", Record.name after.Files["a"])

    [<TestMethod>]
    member _.DeletingAFileRemovesIt() =
        let created = Projection.apply Projection.empty (FileCreated(file "a" "notes.txt" "/"))
        let after = Projection.apply created (FileDeleted(file "a" "notes.txt" "/"))

        Assert.AreEqual<int>(0, after.Files.Count)

    [<TestMethod>]
    member _.ChangingAttributesReplacesThemWholesale() =
        let before = populated.Files["a"].Attributes
        let after = Map.add "tag" (Value.Text "work") before

        let projection = Projection.apply populated (AttributesChanged("a", before, after))

        Assert.AreEqual<string>("work", Attributes.text projection.Files["a"].Attributes "tag")

    [<TestMethod>]
    member _.ChangingContentReplacesTheHash() =
        let projection = Projection.apply populated (ContentChanged("a", Some "old", Some "new"))

        Assert.AreEqual<string option>(Some "new", projection.Files["a"].Content)

    [<TestMethod>]
    member _.UnbindingAVariableRemovesIt() =
        let projection = Projection.apply populated (VariableChanged("v", Some(Value.Number 1.0), None))

        Assert.IsFalse(projection.Variables.ContainsKey "v")

    [<TestMethod>]
    member _.MovingChangesTheLocation() =
        let projection =
            Projection.apply
                populated
                (LocationChanged({ Folder = "/"; View = None }, { Folder = "/documents"; View = None }))

        Assert.AreEqual<string>("/documents", projection.Location.Folder)

    /// An event naming a record that is not there is ignored, not an error. A replay
    /// that could fail would be a log that could not be read back.
    [<TestMethod>]
    member _.AnEventForAMissingRecordChangesNothing() =
        let projection = Projection.apply Projection.empty (ContentChanged("nobody", None, Some "x"))

        Assert.AreEqual<int>(0, projection.Files.Count)

    // ------------------------------------------------------------ The two properties

    /// Applying an event and then its inverse lands exactly where it started.
    [<TestMethod>]
    member _.EveryEventInvertsBackToWhereItStarted() =
        for event in everyEventShape () do
            let after = Projection.apply populated event
            let back = Projection.apply after (Projection.invert event)

            Assert.AreEqual<Map<FileId, FileRecord>>(populated.Files, back.Files, sprintf "Files after %A" event)
            Assert.AreEqual<Map<string, Value>>(populated.Variables, back.Variables, sprintf "Variables after %A" event)
            Assert.AreEqual<Location>(populated.Location, back.Location, sprintf "Location after %A" event)

    /// Inverting twice is the identity, which is what makes redo the same mechanism as
    /// undo rather than a second one.
    [<TestMethod>]
    member _.InvertingTwiceIsTheOriginalEvent() =
        for event in everyEventShape () do
            Assert.AreEqual<Event>(event, Projection.invert (Projection.invert event))

    /// A transaction's inverse is its events inverted *and reversed*: a later event can
    /// depend on an earlier one having happened, so undoing them in order would try to
    /// remove a folder before the file in it.
    [<TestMethod>]
    member _.ATransactionInvertsInReverseOrder() =
        let events =
            [ FileCreated(folder "b" "documents" "/")
              FileCreated(file "a" "notes.txt" "/documents") ]

        let inverted = Projection.invertAll events

        match inverted with
        | [ FileDeleted first; FileDeleted second ] ->
            Assert.AreEqual<string>("notes.txt", Record.name first, "The file should be removed first.")
            Assert.AreEqual<string>("documents", Record.name second)
        | other -> Assert.Fail(sprintf "Expected two deletions, got %A" other)

    /// <summary>
    /// The identity above holds for events that describe a change that happened, and
    /// this is what that excludes.
    /// </summary>
    /// <remarks>
    /// Creating a record that is already there is not a creation, so its inverse
    /// deletes something the creation never added. Nothing in the core produces such
    /// an event — commands read the projection before describing their change, and
    /// the store rejects this particular one outright — but the property is worth
    /// stating as a condition rather than leaving it to look unconditional.
    /// </remarks>
    [<TestMethod>]
    member _.RecreatingAnExistingRecordIsNotInvertible() =
        let event = FileCreated(file "a" "notes.txt" "/")

        let back = Projection.apply (Projection.apply populated event) (Projection.invert event)

        Assert.IsFalse(back.Files.ContainsKey "a", "The inverse removed a record the event never added.")

    [<TestMethod>]
    member _.ApplyingATransactionAndItsInverseLandsWhereItStarted() =
        let events =
            [ FileCreated(folder "s" "scratch" "/")
              FileCreated(file "c" "note.txt" "/scratch")
              VariableChanged("v", Some(Value.Number 1.0), Some(Value.Number 2.0)) ]

        let after = Projection.applyAll populated events
        let back = Projection.applyAll after (Projection.invertAll events)

        Assert.AreEqual<Map<FileId, FileRecord>>(populated.Files, back.Files)
        Assert.AreEqual<Map<string, Value>>(populated.Variables, back.Variables)
