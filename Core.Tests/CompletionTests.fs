namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>What the text could continue as.</summary>
/// <remarks>
/// Phone keyboards have no Tab key, so this is the only completion most users will
/// have. It is over the projection now rather than over a directory on disk, which is
/// why it can complete a name in a folder that only exists in this tab.
/// </remarks>
[<TestClass>]
type CompletionTests() =

    let texts (harness: Harness) (input: string) =
        harness.Session.Complete input |> List.map (fun c -> c.Text)

    /// Offering every command for an empty line is noise, and the page shows its
    /// suggestion chips in that state instead.
    [<TestMethod>]
    member _.NothingTypedOffersNothing() =
        let harness = seeded ()

        Assert.AreEqual<int>(0, (texts harness "").Length)

    [<TestMethod>]
    member _.TheFirstWordCompletesACommand() =
        let harness = seeded ()
        let suggestions = texts harness "c"

        assertContains "cat" suggestions
        assertContains "cd" suggestions
        assertContains "cp" suggestions
        assertContains "clear" suggestions

    /// The words the page handles itself are in the same list, so completion has one
    /// source rather than the page and the core each having half of it.
    [<TestMethod>]
    member _.ThePagesOwnWordsCompleteToo() =
        let harness = seeded ()

        assertContains "help" (texts harness "he")

    [<TestMethod>]
    member _.AWordAfterAPipeIsStillACommand() =
        let harness = seeded ()

        assertContains "count" (texts harness "ls | c")

    /// Phase 5: after `else`, after `try` and inside a parenthesis, a stage starts, so
    /// the word names a command.
    [<TestMethod>]
    member _.AWordThatStartsAStageIsACommand() =
        let harness = seeded ()

        for line in [ "cat x else c"; "try c"; "ls | try c"; "first (c"; "echo (ls | c" ] do
            assertContains "count" (texts harness line)

    [<TestMethod>]
    member _.TryIsOfferedWhereACommandIsWritten() =
        let harness = seeded ()

        assertContains "try" (texts harness "tr")
        assertContains "try" (texts harness "ls | tr")
        Assert.IsFalse(List.contains "try" (texts harness "echo tr"))

    /// `else` needs two letters, because `e` alone is more often a file name.
    [<TestMethod>]
    member _.ElseIsOfferedWhereAnArgumentIsWritten() =
        let harness = seeded ()

        assertContains "else" (texts harness "cat x el")
        Assert.IsFalse(List.contains "else" (texts harness "cat x e"))
        Assert.IsFalse(List.contains "else" (texts harness "el"))

    [<TestMethod>]
    member _.ADollarCompletesAVariable() =
        let harness = seeded ()
        harness.Run "set greeting hello" |> ignore
        harness.Run "set other 1" |> ignore

        Assert.AreEqual<string list>([ "$greeting" ], texts harness "echo $gr")

    [<TestMethod>]
    member _.ALaterWordCompletesANameInTheCurrentFolder() =
        let harness = seeded ()
        let suggestions = texts harness "cat re"

        Assert.AreEqual<string list>([ "readme.txt" ], suggestions)

    /// A folder completes with a separator, so the next tap continues into it.
    [<TestMethod>]
    member _.AFolderCompletesWithASeparator() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "documents/" ], texts harness "cd doc")

    [<TestMethod>]
    member _.APathCompletesInsideTheFolderItNames() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "documents/notes.txt" ], texts harness "cat documents/no")

    [<TestMethod>]
    member _.AnUnknownFolderCompletesToNothing() =
        let harness = seeded ()

        Assert.AreEqual<int>(0, (texts harness "cat nowhere/x").Length)

    /// Completion follows the session, because it reads the same projection every
    /// other command does.
    [<TestMethod>]
    member _.CompletionFollowsTheCurrentFolder() =
        let harness = seeded ()
        harness.Run "cd documents" |> ignore

        Assert.AreEqual<string list>([ "notes.txt" ], texts harness "cat no")

    [<TestMethod>]
    member _.CompletionSeesAFileMadeThisSession() =
        let harness = seeded ()
        harness.Run "write invented.txt x" |> ignore

        Assert.AreEqual<string list>([ "invented.txt" ], texts harness "cat inv")

    /// The start index is where the word began, so the client replaces a word rather
    /// than having to work out where it started.
    [<TestMethod>]
    member _.ACompletionSaysWhereTheWordBegan() =
        let harness = seeded ()

        match harness.Session.Complete "cat re" with
        | [ completion ] ->
            Assert.AreEqual<int>(4, completion.Start)
            Assert.AreEqual<string>("file", completion.Kind)
        | other -> Assert.Fail(sprintf "Expected one completion, got %A" other)

    // ------------------------------------------------------- expressions (Phase 3)

    /// <summary>Operators are offered where an expression is plainly being written.</summary>
    /// <remarks>
    /// The test is a `$` earlier in the stage. A predicate names its row, so one is
    /// always there, and without it `cat no` would offer `not` beside `notes.txt`.
    /// </remarks>
    [<TestMethod>]
    member _.AnOperatorIsOfferedAfterAnOperand() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "eq" ], texts harness "ls | where $row.kind e")

    [<TestMethod>]
    member _.AnOperatorIsOfferedAfterACompleteComparison() =
        let harness = seeded ()

        assertContains "and" (texts harness "ls | where $row.kind eq folder a")

    [<TestMethod>]
    member _.NoOperatorIsOfferedWhereAPathIsBeingWritten() =
        let harness = seeded ()
        harness.Run "cd documents" |> ignore

        Assert.AreEqual<string list>([ "notes.txt" ], texts harness "cat no")

    [<TestMethod>]
    member _.TwoOperatorsInARowAreNotOffered() =
        let harness = seeded ()

        Assert.AreEqual<int>(0, (texts harness "ls | where $row.kind eq n").Length)

    /// A member reads a column, so what follows the stop is a column name.
    [<TestMethod>]
    member _.AMemberCompletesToAColumn() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "$row.kind" ], texts harness "ls | where $row.ki")

    [<TestMethod>]
    member _.AMemberOffersTheAttributesThingsHereCarry() =
        let harness = seeded ()
        harness.Run "attr readme.txt mood=good" |> ignore

        assertContains "$row.mood" (texts harness "ls | where $row.m")

    /// `row` is never in the projection: it exists only while a predicate is running,
    /// and it is the one variable a user writes without having bound it.
    [<TestMethod>]
    member _.RowIsOfferedAlthoughNothingBoundIt() =
        let harness = seeded ()

        assertContains "$row" (texts harness "ls | where $r")

    // ------------------------------------------------------------ Places (Phase 4)

    /// After `cd` the word names somewhere you can be, so a file that is not a place
    /// is not an answer worth offering.
    [<TestMethod>]
    member _.CdOffersOnlyPlaces() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "documents/"; "examples/"; "projects/" ], texts harness "cd ")

    [<TestMethod>]
    member _.OtherCommandsStillOfferEveryName() =
        let harness = seeded ()

        assertContains "readme.txt" (texts harness "cat ")

    /// A saved view is a place without being a path, so it completes as its own name
    /// rather than with a separator after it.
    [<TestMethod>]
    member _.CdOffersASavedView() =
        let harness = seeded ()
        harness.Run "save-view weekend $row.kind eq folder" |> ignore

        assertContains "weekend" (texts harness "cd we")
