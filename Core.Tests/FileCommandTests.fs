namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>cat, write, rm, cp, pwd, mkdir, attr and save.</summary>
/// <remarks>
/// The port of `Execution.Tests/FileAndVariableCommandTests.cs`, plus the two commands
/// the attribute filesystem adds. Undo is no longer a method on each command: every one
/// of these is undone by the same mechanism, which is why the undo cases read the same
/// whatever they are undoing.
/// </remarks>
[<TestClass>]
type FileCommandTests() =

    /// `/documents/`, `/empty/` and `/notes.txt` holding "hello".
    let harness () =
        Harness(
            [ { Name = "documents"; Folder = "/"; Content = None }
              { Name = "empty"; Folder = "/"; Content = None }
              { Name = "notes.txt"; Folder = "/"; Content = Some "hello" } ]
        )

    // ------------------------------------------------------------------------- cat

    [<TestMethod>]
    member _.CatReturnsTheFileAsText() =
        let harness = harness ()

        Assert.AreEqual<Value>(Value.Text "hello", harness.Run "cat notes.txt")

    [<TestMethod>]
    member _.CatAcceptsAPipedPath() =
        let harness = harness ()

        Assert.AreEqual<string>("hello", harness.Text "echo notes.txt | cat")

    [<TestMethod>]
    member _.CatOfAMissingFileIsReported() =
        let harness = harness ()

        StringAssert.Contains(harness.Error "cat nowhere.txt", "File does not exist")

    [<TestMethod>]
    member _.CatOfADirectoryIsReported() =
        let harness = harness ()

        StringAssert.Contains(harness.Error "cat documents", "directory, not a file")

    /// A record can have attributes and no content at all, which `save` makes. That is
    /// empty text rather than a missing file.
    [<TestMethod>]
    member _.CatOfARecordWithNoContentIsEmpty() =
        let harness = harness ()
        harness.Run "save <note name=todo/>" |> ignore

        Assert.AreEqual<Value>(Value.Text "", harness.Run "cat todo")

    // ----------------------------------------------------------------------- write

    [<TestMethod>]
    member _.WriteCreatesAFileAndReturnsIt() =
        let harness = harness ()

        match harness.Run "write new.txt content" with
        | Value.File file -> Assert.AreEqual<string>("new.txt", file.Name)
        | other -> Assert.Fail(sprintf "Expected a file, got %A" other)

        Assert.AreEqual<string>("content", harness.Content "new.txt")

    [<TestMethod>]
    member _.WriteTakesItsTextFromThePipe() =
        let harness = harness ()
        harness.Run "cat notes.txt | write copy.txt" |> ignore

        Assert.AreEqual<string>("hello", harness.Content "copy.txt")

    [<TestMethod>]
    member _.WriteIntoAMissingDirectoryIsReported() =
        let harness = harness ()

        StringAssert.Contains(harness.Error "write nowhere/x.txt text", "Directory does not exist")

    [<TestMethod>]
    member _.WriteOverAnExistingFileReplacesItsContents() =
        let harness = harness ()
        harness.Run "write notes.txt replaced" |> ignore

        Assert.AreEqual<string>("replaced", harness.Content "notes.txt")

    [<TestMethod>]
    member _.WriteOverADirectoryIsReported() =
        let harness = harness ()

        StringAssert.Contains(harness.Error "write documents text", "directory, not a file")

    [<TestMethod>]
    member _.UndoOfWriteRestoresThePreviousContents() =
        let harness = harness ()
        harness.Run "write notes.txt replaced" |> ignore

        harness.Run "undo" |> ignore

        Assert.AreEqual<string>("hello", harness.Content "notes.txt")

    [<TestMethod>]
    member _.UndoOfWriteRemovesAFileThatDidNotExist() =
        let harness = harness ()
        harness.Run "write fresh.txt text" |> ignore

        harness.Run "undo" |> ignore

        Assert.IsFalse(harness.Exists "fresh.txt")

    /// A file's kind is guessed from its name, so a listing can tell a table from a
    /// note without opening either.
    [<TestMethod>]
    member _.WriteInfersTheKindFromTheName() =
        let harness = harness ()
        harness.Run "write data.csv rows" |> ignore

        Assert.AreEqual<string>("csv", harness.Attribute "data.csv" "kind")

    // -------------------------------------------------------------------------- rm

    [<TestMethod>]
    member _.RmDeletesAFile() =
        let harness = harness ()
        harness.Run "rm notes.txt" |> ignore

        Assert.IsFalse(harness.Exists "notes.txt")

    [<TestMethod>]
    member _.RmDeletesAnEmptyDirectory() =
        let harness = harness ()
        harness.Run "rm empty" |> ignore

        Assert.IsFalse(harness.Exists "empty")

    /// Undoing a recursive delete means restoring a whole tree, and a shell that
    /// deletes trees on one word is not one to try out on a phone.
    [<TestMethod>]
    member _.RmRefusesANonEmptyDirectory() =
        let harness = harness ()
        harness.Run "write documents/a.txt a" |> ignore

        StringAssert.Contains(harness.Error "rm documents", "not empty")
        Assert.IsTrue(harness.Exists "documents")

    [<TestMethod>]
    member _.RmOfNothingIsReported() =
        let harness = harness ()

        StringAssert.Contains(harness.Error "rm ghost", "Nothing exists")

    [<TestMethod>]
    member _.RmRefusesTheCurrentDirectory() =
        let harness = harness ()
        harness.Run "cd documents" |> ignore

        StringAssert.Contains(harness.Error "rm /documents", "Cannot delete the current directory")

    [<TestMethod>]
    member _.UndoOfRmRestoresTheFileWithItsContents() =
        let harness = harness ()
        harness.Run "rm notes.txt" |> ignore

        harness.Run "undo" |> ignore

        Assert.AreEqual<string>("hello", harness.Content "notes.txt")

    [<TestMethod>]
    member _.UndoOfRmRestoresTheDirectory() =
        let harness = harness ()
        harness.Run "rm empty" |> ignore

        harness.Run "undo" |> ignore

        Assert.IsTrue(harness.Exists "empty")

    /// The record comes back with everything it had, because the event carried the
    /// whole record rather than a path.
    [<TestMethod>]
    member _.UndoOfRmRestoresTheAttributesToo() =
        let harness = harness ()
        harness.Run "attr notes.txt tag=work" |> ignore
        harness.Run "rm notes.txt" |> ignore

        harness.Run "undo" |> ignore

        Assert.AreEqual<string>("work", harness.Attribute "notes.txt" "tag")

    // -------------------------------------------------------------------- mkdir/cd

    [<TestMethod>]
    member _.MkdirRefusesAnExistingName() =
        let harness = harness ()

        StringAssert.Contains(harness.Error "mkdir documents", "Target directory already exists")

    /// Names are unique across files and folders together (decision 0016).
    [<TestMethod>]
    member _.MkdirRefusesTheNameOfAFile() =
        let harness = harness ()

        StringAssert.Contains(harness.Error "mkdir notes.txt", "already exists")

    [<TestMethod>]
    member _.PwdReturnsTheCurrentDirectory() =
        let harness = harness ()
        harness.Run "cd documents" |> ignore

        Assert.AreEqual<Value>(Value.Text "/documents", harness.Run "pwd")

    /// Moving nowhere is not a change, so the line commits nothing and `undo` reaches
    /// past it.
    [<TestMethod>]
    member _.CdToWhereYouAlreadyAreCommitsNothing() =
        let harness = harness ()
        harness.Run "mkdir alpha" |> ignore
        harness.Run "cd ." |> ignore

        harness.Run "undo" |> ignore

        Assert.IsFalse(harness.Exists "alpha", "The undo should have reached past the cd.")

    // -------------------------------------------------------------------------- cp

    [<TestMethod>]
    member _.CpCopiesAFileIntoADirectory() =
        let harness = harness ()
        harness.Run "cp notes.txt documents" |> ignore

        Assert.AreEqual<string>("hello", harness.Content "/documents/notes.txt")
        Assert.AreEqual<string>("hello", harness.Content "/notes.txt")

    [<TestMethod>]
    member _.CpOfAMissingFileIsReported() =
        let harness = harness ()

        StringAssert.Contains(harness.Error "cp ghost.txt documents", "File does not exist")

    [<TestMethod>]
    member _.CpIntoAMissingDirectoryIsReported() =
        let harness = harness ()

        StringAssert.Contains(harness.Error "cp notes.txt nowhere", "Target directory does not exist")

    [<TestMethod>]
    member _.CpRefusesToOverwrite() =
        let harness = harness ()
        harness.Run "cp notes.txt documents" |> ignore

        StringAssert.Contains(harness.Error "cp notes.txt documents", "Target file already exists")

    // ------------------------------------------------------------------------ attr

    [<TestMethod>]
    member _.AttrWithNoAssignmentsListsTheAttributes() =
        let harness = harness ()

        let names = harness.Column "attr notes.txt" "name"
        let values = harness.Column "attr notes.txt" "value"

        assertContains "name" names
        assertContains "kind" names
        assertContains "folder" names
        assertContains "notes.txt" values
        assertContains "text" values

    /// Decision 0017: `attr` takes names it has never heard of, which is the whole
    /// reason `name=value` is data rather than parameter binding.
    [<TestMethod>]
    member _.AttrSetsArbitraryNames() =
        let harness = harness ()

        match harness.Run "attr notes.txt tag=work due=2026-10-01" with
        | Value.File file -> Assert.AreEqual<string>("notes.txt", file.Name)
        | other -> Assert.Fail(sprintf "Expected a file, got %A" other)

        Assert.AreEqual<string>("work", harness.Attribute "notes.txt" "tag")
        Assert.AreEqual<string>("2026-10-01", harness.Attribute "notes.txt" "due")

    [<TestMethod>]
    member _.UndoOfAttrRestoresThePreviousAttributes() =
        let harness = harness ()
        harness.Run "attr notes.txt tag=work" |> ignore

        harness.Run "undo" |> ignore

        Assert.AreEqual<string>("", harness.Attribute "notes.txt" "tag")

    /// `name` and `kind` are the user's; `folder`, `created` and `modified` are the
    /// runtime's, and writing one would make the record disagree with itself.
    [<DataTestMethod>]
    [<DataRow("folder")>]
    [<DataRow("created")>]
    [<DataRow("modified")>]
    member _.AttrRefusesTheRuntimesOwnAttributes(name: string) =
        let harness = harness ()

        StringAssert.Contains(harness.Error $"attr notes.txt {name}=x", "set by the terminal")

    [<TestMethod>]
    member _.AttrCanRenameAndRetype() =
        let harness = harness ()
        harness.Run "attr notes.txt name=renamed.txt kind=note" |> ignore

        Assert.IsTrue(harness.Exists "renamed.txt")
        Assert.AreEqual<string>("note", harness.Attribute "renamed.txt" "kind")

    [<TestMethod>]
    member _.AttrRefusesToRenameOntoAnExistingName() =
        let harness = harness ()
        harness.Run "write other.txt x" |> ignore

        StringAssert.Contains(harness.Error "attr notes.txt name=other.txt", "already exists")

    /// A command that declares no assignments says so, rather than ignoring them.
    [<TestMethod>]
    member _.ACommandThatTakesNoAssignmentsSaysSo() =
        let harness = harness ()

        StringAssert.Contains(harness.Error "progress steps=20", "does not take 'steps=' assignments")

    // ------------------------------------------------------------------------ save

    /// <summary>A tag with attributes is a file record (decision 0013).</summary>
    /// <remarks>
    /// This is the claim made executable: the type name becomes the kind, the `name`
    /// attribute becomes the name, and everything else is carried across as it stands.
    /// </remarks>
    [<TestMethod>]
    member _.SaveTurnsATagIntoAFile() =
        let harness = harness ()

        match harness.Run "save <note name=todo due=2026-10-01/>" with
        | Value.File file ->
            Assert.AreEqual<string>("todo", file.Name)
            Assert.AreEqual<string>("note", file.Kind)
        | other -> Assert.Fail(sprintf "Expected a file, got %A" other)

        Assert.AreEqual<string>("2026-10-01", harness.Attribute "todo" "due")

    [<TestMethod>]
    member _.SaveNeedsAName() =
        let harness = harness ()

        StringAssert.Contains(harness.Error "save <note due=2026-10-01/>", "needs a 'name' attribute")

    [<TestMethod>]
    member _.SaveRefusesAnExistingName() =
        let harness = harness ()

        StringAssert.Contains(harness.Error "save <note name=notes.txt/>", "already exists")

    [<TestMethod>]
    member _.SaveNeedsATagRatherThanText() =
        let harness = harness ()

        StringAssert.Contains(harness.Error "save hello", "needs a tag")

    [<TestMethod>]
    member _.SaveAcceptsAPipedTag() =
        let harness = harness ()
        harness.Run "echo <note name=piped/> | save" |> ignore

        Assert.IsTrue(harness.Exists "piped")

    [<TestMethod>]
    member _.UndoOfSaveRemovesTheRecord() =
        let harness = harness ()
        harness.Run "save <note name=todo/>" |> ignore

        harness.Run "undo" |> ignore

        Assert.IsFalse(harness.Exists "todo")
