namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>Phase 1's acceptance list, run from a fresh session.</summary>
/// <remarks>
/// The same lines the browser check script runs against the published page, so the two
/// cannot drift: if the core changes an answer, this fails here long before anyone
/// opens a browser. Each case is written as the session would show it.
/// </remarks>
[<TestClass>]
type AcceptanceTests() =

    [<TestMethod>]
    member _.AFreshSessionHasSomethingToLookAt() =
        let harness = seeded ()

        Assert.AreEqual<string>("documents examples guide projects readme.txt", harness.Names "ls")

    /// A failed line leaves no trace, whatever its earlier stages managed.
    [<TestMethod>]
    member _.AFailedLineIsAtomic() =
        let harness = seeded ()

        Assert.AreEqual<string>("Directory does not exist : nowhere", harness.Error "mkdir a | in nowhere")
        Assert.AreEqual<string>("documents examples guide projects readme.txt", harness.Names "ls")

    [<TestMethod>]
    member _.MkdirUndoRedoHistory() =
        let harness = seeded ()

        Assert.AreEqual<string>("alpha", harness.Text "mkdir alpha")
        Assert.AreEqual<string>("alpha documents examples guide projects readme.txt", harness.Names "ls")
        Assert.AreEqual<string>("Undone: mkdir alpha", harness.Text "undo")
        Assert.AreEqual<string>("documents examples guide projects readme.txt", harness.Names "ls")
        Assert.AreEqual<string>("Redone: mkdir alpha", harness.Text "redo")

        // The `ls` lines committed nothing, so the history holds four lines and not six.
        Assert.AreEqual<string list>([ "seed"; "mkdir alpha"; "mkdir alpha"; "mkdir alpha" ], harness.History())
        Assert.AreEqual<string list>([], harness.Undone())

    [<TestMethod>]
    member _.WritingTwiceAndUndoingRestoresTheFirstText() =
        let harness = seeded ()

        Assert.AreEqual<string>("note.txt", harness.Text "write note.txt first")
        Assert.AreEqual<string>("note.txt", harness.Text "write note.txt second")
        Assert.AreEqual<string>("Undone: write note.txt second", harness.Text "undo")
        Assert.AreEqual<string>("first", harness.Text "read note.txt")

    [<TestMethod>]
    member _.AttributesAreWrittenAndReadBack() =
        let harness = seeded ()
        harness.Run "write note.txt first" |> ignore

        Assert.AreEqual<string>("note.txt", harness.Text "attr note.txt tag=work")

        let names = harness.Column "attr note.txt" "name"
        let values = harness.Column "attr note.txt" "value"

        CollectionAssert.AreEqual(
            [| "created"; "folder"; "kind"; "modified"; "name"; "tag" |], names |> Array.ofList)

        assertContains "note.txt" values
        assertContains "text" values
        assertContains "work" values

    [<TestMethod>]
    member _.ATagIsSavedAsAFile() =
        let harness = seeded ()

        Assert.AreEqual<string>("todo", harness.Text "save <note name=todo due=2026-10-01/>")
        Assert.AreEqual<string>("", harness.Text "read todo")
        Assert.AreEqual<string>("note", harness.Attribute "todo" "kind")
        Assert.AreEqual<string>("2026-10-01", harness.Attribute "todo" "due")

    /// Decision 0007: a path in a tag attribute needs no quotes, and the tag still
    /// reads back the way it was written.
    [<TestMethod>]
    member _.APathInATagReadsBackUnchanged() =
        let harness = seeded ()

        Assert.AreEqual<string>("<file path=documents/notes.txt/>", harness.Text "<file path=documents/notes.txt/>")

    [<TestMethod>]
    member _.ANegativeNumberIsANumber() =
        let harness = seeded ()

        Assert.AreEqual<string>("-5", harness.Text "echo -5")
        Assert.AreEqual<Value>(Value.Number -5.0, harness.Run "echo -5")

    [<TestMethod>]
    member _.TwoSetsUndoneTwiceLeaveTheVariableUnbound() =
        let harness = seeded ()
        harness.Run "set v 1" |> ignore
        harness.Run "set v 2" |> ignore
        harness.Run "undo" |> ignore
        harness.Run "undo" |> ignore

        StringAssert.Contains(harness.Error "echo $v", "Unknown variable")

    [<TestMethod>]
    member _.ProgressRunsToAHundred() =
        let harness = seeded ()

        Assert.AreEqual<string>("100", harness.Text "progress 3 1")
