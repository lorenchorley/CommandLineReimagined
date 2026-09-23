namespace CommandLineReimagined.Core.Tests

open System.Threading
open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// Command names at the head of a line and after a pipe (stream A).
[<TestClass>]
type CommandCompletionTests() =

    let items (harness: Harness) (line: string) =
        Async.RunSynchronously(harness.Session.Complete(line, line.Length)).Items

    let texts (harness: Harness) (line: string) =
        items harness line |> List.map (fun c -> c.Text)

    let detailOf (harness: Harness) (line: string) (text: string) =
        match items harness line |> List.tryFind (fun c -> c.Text = text) with
        | Some completion -> completion.Detail
        | None -> Assert.Fail(sprintf "'%s' offered nothing called '%s'." line text); None

    /// What a provider is given for a line, built the way the session builds it.
    let request (harness: Harness) (line: string) =
        let source =
            { Projection = harness.Projection
              Preview = fun _ _ -> async.Return None
              Cancel = CancellationToken.None
              Cache = ShapeCache() }

        Completion.request harness.Session.Commands source line line.Length CancellationToken.None

    /// The smoke test the foundation leaves: the stream fills in the rest.
    [<TestMethod>]
    member _.ACommandNameCompletes() =
        let harness = seeded ()

        assertContains "where" (texts harness "wh")

    // ------------------------------------------------------ 7: what a chip says

    /// Finding 7: a command chip says what the command does before it is tapped.
    [<TestMethod>]
    member _.ACommandChipCarriesItsDescription() =
        let harness = seeded ()

        Assert.AreEqual<string option>(Some "Keep the rows a predicate is true for", detailOf harness "wh" "where")

    [<TestMethod>]
    member _.EveryCommandChipHasADetail() =
        let harness = seeded ()

        for completion in items harness "c" |> List.filter (fun c -> c.Kind = "command" && c.Text <> "clear") do
            Assert.IsTrue(completion.Detail.IsSome, sprintf "'%s' has no detail." completion.Text)

    /// A command chip replaces the word, and only the word, when the cursor is inside it.
    [<TestMethod>]
    member _.ACommandChipReplacesTheWordUnderTheCursor() =
        let harness = seeded ()
        let result = Async.RunSynchronously(harness.Session.Complete("wh | count", 2))

        match result.Items |> List.tryFind (fun c -> c.Text = "where") with
        | Some completion ->
            Assert.AreEqual<int>(0, completion.Start)
            Assert.AreEqual<int>(2, completion.End)
        | None -> Assert.Fail "Expected 'where'."

    // ---------------------------------------------------- 8: after a pipe

    /// Finding 8: straight after a pipe, the commands that take what the pipe carries.
    [<TestMethod>]
    member _.AfterAPipeTheCommandsThatTakeThePipeAreOffered() =
        let harness = seeded ()
        let offered = texts harness "ls | "

        for name in [ "where"; "sort"; "count"; "select"; "set" ] do
            assertContains name offered

        for name in [ "mkdir"; "ls"; "pwd"; "undo"; "clear" ] do
            assertDoesNotContain name offered

    /// A listing is a table, and a table is not a name, so the commands that take a
    /// path or a place from the pipe are not offered after one: `ls | cat` and
    /// `ls | rm` are faults. After text they are, because the text may be a name.
    [<TestMethod>]
    member _.AfterATableTheCommandsThatTakeAPathAreNotOffered() =
        let harness = seeded ()

        for line in [ "ls | "; "ls | where $row.kind eq text | " ] do
            let offered = texts harness line

            for name in [ "cat"; "rm"; "cd"; "run"; "attr"; "from-csv"; "from-xml" ] do
                assertDoesNotContain name offered

            for name in [ "where"; "count"; "write"; "to-csv" ] do
                assertContains name offered

        let afterText = texts harness "echo readme.txt | "

        for name in [ "cat"; "rm"; "cd" ] do
            assertContains name afterText

    [<TestMethod>]
    member _.EveryCommandOfferedAfterAPipeTakesThePipe() =
        let harness = seeded ()
        let specs = harness.Session.Commands

        for line in [ "ls | "; "ls | s"; "echo (ls | c" ] do
            for completion in items harness line |> List.filter (fun c -> c.Kind = "command") do
                let spec = specs |> List.find (fun spec -> spec.Name = completion.Text)
                Assert.IsTrue(CommandCompletion.takesThePipe spec, sprintf "'%s' offered '%s'." line spec.Name)

    /// `try` starts a stage anywhere a command does, so it stays after a pipe.
    [<TestMethod>]
    member _.TryIsStillOfferedAfterAPipe() =
        let harness = seeded ()

        assertContains "try" (texts harness "ls | tr")
        assertContains "try" (texts harness "ls | ")

    /// The head of a line still offers every command, whether it takes a pipe or not.
    [<TestMethod>]
    member _.AtTheHeadOfALineEveryCommandIsOffered() =
        let harness = seeded ()

        assertContains "mkdir" (texts harness "mk")
        assertContains "clear" (texts harness "cl")

    [<TestMethod>]
    member _.AnEmptyLineStillOffersNothing() =
        let harness = seeded ()

        Assert.AreEqual<int>(0, (items harness "").Length)
        Assert.AreEqual<int>(0, (items harness "  ").Length)

    // ------------------------------------------- 9: keywords and near misses

    /// Finding 9: `delete` finds `rm` by its keywords, and the chip says why.
    [<TestMethod>]
    member _.AKeywordFindsTheCommand() =
        let harness = seeded ()

        assertContains "rm" (texts harness "delete")
        Assert.AreEqual<string option>(Some "rm · matches \"delete\"", detailOf harness "delete" "rm")

    /// A keyword is matched as it is typed, so `del` finds `rm` before `delete` is done.
    [<TestMethod>]
    member _.AKeywordMatchesAsItIsTyped() =
        let harness = seeded ()

        Assert.AreEqual<string option>(Some "rm · matches \"delete\"", detailOf harness "del" "rm")

    /// Finding 9: a slip is corrected: `lss` offers `ls`, and a swapped pair counts one.
    [<TestMethod>]
    member _.ANearMissFindsTheCommand() =
        let harness = seeded ()

        assertContains "ls" (texts harness "lss")
        assertContains "sort" (texts harness "ls | sotr")
        assertContains "where" (texts harness "ls | whree")

    /// Prefix matches first, then keyword matches, then near misses.
    [<TestMethod>]
    member _.PrefixesComeBeforeKeywordsWhichComeBeforeNearMisses() =
        let harness = seeded ()
        let offered = texts harness "whe"

        // `where` starts with the word; `pwd` has the keyword `where`.
        Assert.AreEqual<string>("where", List.head offered)
        Assert.AreEqual<string option>(Some "pwd · matches \"where\"", detailOf harness "whe" "pwd")
        let index name = offered |> List.findIndex ((=) name)
        Assert.IsTrue(index "where" < index "pwd", String.concat " " offered)

        let offered = texts harness "cat"
        Assert.AreEqual<string>("cat", List.head offered)

    [<TestMethod>]
    member _.AKeywordMatchRanksAboveANearMiss() =
        let harness = seeded ()
        // `mkdir` has the keyword `make`; `take` is one slip from `make`.
        let offered = texts harness "make"
        let index name = offered |> List.findIndex ((=) name)

        Assert.IsTrue(index "mkdir" < index "take", String.concat " " offered)

    /// Two letters start too many keywords to mean anything: only names complete.
    [<TestMethod>]
    member _.OneOrTwoLettersMatchNamesOnly() =
        let harness = seeded ()

        for prefix in [ "d"; "se"; "ct" ] do
            for completion in items harness prefix |> List.filter (fun c -> c.Kind = "command") do
                StringAssert.StartsWith(completion.Text, prefix)

        Assert.AreEqual<string list>([ "select"; "set" ], texts harness "ls | se")

    // ------------------------------------------------- 11: help's argument

    /// `help `'s argument is a command's name (`Takes.CommandName`).
    [<TestMethod>]
    member _.HelpsArgumentTakesACommandName() =
        let harness = seeded ()

        match (request harness "help ").Context.Place with
        | Place.Argument(stage, Slot.Parameter parameter) ->
            Assert.AreEqual<string>("help", stage.Name)
            Assert.AreEqual<Takes>(Takes.CommandName, parameter.Takes)
        | other -> Assert.Fail(sprintf "Expected help's argument, read %s." (Context.describe other))

    /// <summary>Finding 11: what a command-name argument offers is the command names.</summary>
    /// <remarks>
    /// `CommandCompletion.names` is what a `Takes.CommandName` parameter offers. The
    /// argument dispatcher is stream D's, which offers files until it merges.
    /// </remarks>
    [<TestMethod>]
    member _.ACommandNameArgumentOffersTheCommandNames() =
        let harness = seeded ()
        let offered = CommandCompletion.names (request harness "help ") |> List.map (fun c -> c.Text)

        Assert.AreEqual<string list>(harness.Session.Commands |> List.map (fun spec -> spec.Name), offered)
        assertDoesNotContain "clear" offered
        assertDoesNotContain "try" offered

        let offered = CommandCompletion.names (request harness "help wh") |> List.map (fun c -> c.Text)
        Assert.AreEqual<string>("where", List.head offered)

    /// The chips replace the argument being written, not the command before it.
    [<TestMethod>]
    member _.ACommandNameArgumentReplacesTheArgument() =
        let harness = seeded ()

        match CommandCompletion.names (request harness "help wh") with
        | first :: _ ->
            Assert.AreEqual<int>(5, first.Start)
            Assert.AreEqual<int>(7, first.End)
            Assert.AreEqual<string option>(Some "Keep the rows a predicate is true for", first.Detail)
        | [] -> Assert.Fail "Expected command names."

    // ------------------------------------------------------------ Nearest

    [<TestMethod>]
    member _.TheDistanceCountsASwapAsOneEdit() =
        Assert.AreEqual<int>(1, Nearest.distance "sotr" "sort")
        Assert.AreEqual<int>(1, Nearest.distance "lss" "ls")
        Assert.AreEqual<int>(0, Nearest.distance "WHERE" "where")
        Assert.AreEqual<int>(3, Nearest.distance "" "abc")
        Assert.AreEqual<int>(1, Nearest.distance "ab" "BA")

    [<TestMethod>]
    member _.NearestNamesAreWithinTheThresholdAndNearestFirst() =
        let candidates = [ "ls"; "cd"; "sort"; "select"; "where" ]

        Assert.AreEqual<string list>([ "ls" ], Nearest.names candidates "lss")
        Assert.AreEqual<string list>([ "sort" ], Nearest.names candidates "sotr")
        // Two slips are allowed in a longer word, and the one-slip name comes first.
        Assert.AreEqual<string list>([ "select" ], Nearest.names candidates "selcet")
        Assert.AreEqual<string list>([ "select"; "set" ], Nearest.names [ "set"; "select" ] "selet")
        Assert.AreEqual<string list>([], Nearest.names candidates "frobnicate")
        Assert.AreEqual<string list>([], Nearest.names candidates "")
        // A word that is already a candidate was not mistyped.
        Assert.AreEqual<string list>([], Nearest.names candidates "ls")
