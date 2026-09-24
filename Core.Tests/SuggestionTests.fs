namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>A fix becomes the whole corrected line (decision 0044).</summary>
/// <remarks>
/// Phase 10's foundation: whatever finds a mistake says its fix as a replacement of
/// what was written, and the session makes it a line against what the person typed.
/// Stream A's suggestions and fixes are tested in the classes after this one.
/// </remarks>
[<TestClass>]
type FixTests() =

    static let apply source fix = Fix.apply source fix

    [<TestMethod>]
    member _.AReplacementIsMadeWhereTheLineHasTheWord() =
        Assert.AreEqual<string option>(Some "ls -a", apply "lss -a" (Fix.Replace("lss", "ls")))
        Assert.AreEqual<string option>(Some "echo $files", apply "echo $fles" (Fix.Replace("$fles", "$files")))

        Assert.AreEqual<string option>(
            Some "ls | where $row.kind eq folder",
            apply "ls | where kind eq folder" (Fix.Replace("kind eq folder", "$row.kind eq folder"))
        )

    /// A word inside a longer one, or read off a variable, is not the word written.
    [<TestMethod>]
    member _.AReplacementTakesOnlyAWholeWord() =
        Assert.AreEqual<string option>(
            Some "ls | where $row.kinds eq kind",
            apply "ls | where $row.kinds eq knd" (Fix.Replace("knd", "kind"))
        )

        Assert.AreEqual<string option>(
            Some "ls | where $row.kind eq knd",
            apply "ls | where $row.knd eq knd" (Fix.Replace("$row.knd", "$row.kind"))
        )

        Assert.AreEqual<string option>(None, apply "ls | where $row.kinds eq folder" (Fix.Replace("kind", "knd")))

    /// A fix the line has no place for, or that changes nothing, is not offered.
    [<TestMethod>]
    member _.AFixThatMakesNoNewLineIsDropped() =
        Assert.AreEqual<string option>(None, apply "run script.clr" (Fix.Replace("lss", "ls")))
        Assert.AreEqual<string option>(None, apply "ls" (Fix.Line "ls"))
        Assert.AreEqual<string option>(None, apply "ls" (Fix.Replace("", "x")))
        Assert.AreEqual<string option>(Some "ls", apply "lss" (Fix.Line "ls"))

    [<TestMethod>]
    member _.ANoteResolvesToDistinctLines() =
        let note =
            Note.suggestion
                "Did you mean ls?"
                [ Fix.Replace("lss", "ls"); Fix.Line "ls"; Fix.Replace("nowhere", "x") ]

        let resolved = Note.resolve "lss" note

        Assert.AreEqual<string>("suggestion", resolved.Kind)
        Assert.AreEqual<string>("Did you mean ls?", resolved.Text)
        Assert.AreEqual<string list>([ "ls" ], Note.fixLines resolved)

    /// A fault that becomes a value leaves its notes behind (decision 0041).
    [<TestMethod>]
    member _.AFaultAsAValueHasNoNotes() =
        let note = Note.suggestion "Did you mean ls?" [ Fix.Line "ls" ]
        let inner = Fault.create Invalid "inner" |> Fault.withNotes [ note ]
        let fault = Fault.create NotFound "outer" |> Fault.withNotes [ note ] |> Fault.causedBy inner
        let value = Fault.asValue fault

        Assert.AreEqual<Note list>([], value.Notes)
        Assert.AreEqual<Note list>([], value.Cause.Value.Notes)
        Assert.AreEqual<string>("outer", value.Message)

/// What a response says of its own, as its kind, its text and the whole lines it offers.
module private Said =

    let notes (response: Response) =
        response.Notes |> List.map (fun note -> note.Kind, note.Text, Note.fixLines note)

    let message (response: Response) =
        match response.Fault with
        | Some fault -> fault.Message
        | None -> raise (AssertFailedException(sprintf "'%s' was expected to fail, and did not." response.Source))

    /// The fault's message, and each note, of a line expected to fail.
    let failing (harness: Harness) (line: string) =
        let response = harness.Respond line
        message response, notes response

/// <summary>A mistyped command says what was meant beside the fault, not in it (0041, 0044).</summary>
[<TestClass>]
type CommandSuggestionTests() =

    [<TestMethod>]
    member _.AMistypedCommandIsSuggestedWithItsFix() =
        let harness = seeded ()

        Assert.AreEqual<string * (string * string * string list) list>(
            ("Unknown command : lss", [ "suggestion", "Did you mean ls?", [ "ls" ] ]),
            Said.failing harness "lss")

    /// One fix per command named, the rest of the line kept.
    [<TestMethod>]
    member _.EachCommandNamedHasAFix() =
        let harness = seeded ()

        Assert.AreEqual<string * (string * string * string list) list>(
            ("Unknown command : rn", [ "suggestion", "Did you mean in, rm or run?", [ "in"; "rm"; "run" ] ]),
            Said.failing harness "rn")

        Assert.AreEqual<string * (string * string * string list) list>(
            ("Unknown command : cd", [ "suggestion", "Did you mean in?", [ "in documents" ] ]),
            Said.failing harness "cd documents")

        Assert.AreEqual<string * (string * string * string list) list>(
            ("Unknown command : lss", [ "suggestion", "Did you mean ls?", [ "help ls" ] ]),
            Said.failing harness "help lss")

    [<TestMethod>]
    member _.ANameNothingIsNearSaysNothingMore() =
        let harness = seeded ()

        Assert.AreEqual<string * (string * string * string list) list>(
            ("Unknown command : frobnicate", []),
            Said.failing harness "frobnicate")

/// <summary>A predicate that is not a question says how to ask it, and offers the line (0041, 0044).</summary>
[<TestClass>]
type PredicateSuggestionTests() =

    [<TestMethod>]
    member _.APredicateThatNeverReadsTheRowOffersTheRowRead() =
        let harness = seeded ()

        Assert.AreEqual<string * (string * string * string list) list>(
            ("kind eq folder never reads $row, so it is the same for every row.",
             [ "suggestion", "Did you mean $row.kind eq folder?", [ "ls | where $row.kind eq folder" ] ]),
            Said.failing harness "ls | where kind eq folder")

    [<TestMethod>]
    member _.APredicateWithNothingToReadAsAColumnSaysNothingMore() =
        let harness = seeded ()
        harness.Run "set x 1" |> ignore

        Assert.AreEqual<string * (string * string * string list) list>(
            ("$x eq 1 never reads $row, so it is the same for every row.", []),
            Said.failing harness "ls | where $x eq 1")

    /// A column read bare gets the comparison to write; a bare word, the column.
    [<TestMethod>]
    member _.APredicateThatIsNotTrueOrFalseOffersTheQuestion() =
        let harness = seeded ()

        Assert.AreEqual<string * (string * string * string list) list>(
            ("$row.kind is text (folder), not true or false.",
             [ "suggestion", "Compare it: $row.kind eq folder.", [ "ls | where $row.kind eq folder" ] ]),
            Said.failing harness "ls | where $row.kind")

        Assert.AreEqual<string * (string * string * string list) list>(
            ("kind is text (kind), not true or false.",
             [ "suggestion", "Did you mean $row.kind?", [ "ls | where $row.kind" ] ]),
            Said.failing harness "ls | where kind")

    /// The operand is in the line twice, and the fault cannot say which one it was, so
    /// the suggestion stands and no line is offered: the first would be the wrong one.
    [<TestMethod>]
    member _.AnOperandWrittenTwiceOffersNoFix() =
        let harness = seeded ()

        Assert.AreEqual<string * (string * string * string list) list>(
            ("$row.kind is text (folder), not true or false.",
             [ "suggestion", "Compare it: $row.kind eq folder.", [] ]),
            Said.failing harness "ls | where $row.kind eq text or $row.kind")

/// <summary>A missing file or folder names the nearest paths, with a fix for each (0042, 0044).</summary>
[<TestClass>]
type PathSuggestionTests() =

    [<TestMethod>]
    member _.AMissingFileNamesTheNearestPath() =
        let harness = seeded ()

        Assert.AreEqual<string * (string * string * string list) list>(
            ("File does not exist : /notes",
             [ "suggestion", "Did you mean documents/notes.txt?", [ "read documents/notes.txt" ] ]),
            Said.failing harness "read notes")

    [<TestMethod>]
    member _.AMissingFileWithNothingNearSaysNothingMore() =
        let harness = seeded ()

        Assert.AreEqual<string * (string * string * string list) list>(
            ("File does not exist : /nothingnear", []),
            Said.failing harness "read nothingnear")

    /// A folder is looked for among folders only. A file is looked for among every
    /// record: a near name in the folder it was looked in first, then a longer name.
    [<TestMethod>]
    member _.AMissingFolderOffersOnlyFolders() =
        let harness = seeded ()
        harness.Run "mkdir report" |> ignore
        harness.Run "write reports.txt hello" |> ignore

        Assert.AreEqual<string * (string * string * string list) list>(
            ("Directory does not exist : reports", [ "suggestion", "Did you mean report?", [ "in report" ] ]),
            Said.failing harness "in reports")

        Assert.AreEqual<string * (string * string * string list) list>(
            ("File does not exist : /reports",
             [ "suggestion", "Did you mean report or reports.txt?", [ "read report"; "read reports.txt" ] ]),
            Said.failing harness "read reports")

    /// A path is written from where the person is: relative inside the current folder,
    /// absolute outside it.
    [<TestMethod>]
    member _.APathIsWrittenFromTheCurrentFolder() =
        let harness = seeded ()

        Assert.AreEqual<string * (string * string * string list) list>(
            ("File does not exist : /documents/nots.txt",
             [ "suggestion", "Did you mean documents/notes.txt?", [ "read documents/notes.txt" ] ]),
            Said.failing harness "read documents/nots.txt")

        harness.Run "in guide" |> ignore

        Assert.AreEqual<string * (string * string * string list) list>(
            ("File does not exist : /guide/notes",
             [ "suggestion", "Did you mean /documents/notes.txt?", [ "read /documents/notes.txt" ] ]),
            Said.failing harness "read notes")

        Assert.AreEqual<string * (string * string * string list) list>(
            ("File does not exist : /guide/1-strt.txt",
             [ "suggestion", "Did you mean 1-start.txt?", [ "read 1-start.txt" ] ]),
            Said.failing harness "read 1-strt.txt")

    /// A path under a folder that is not there is fixed at the folder, and keeps the rest.
    [<TestMethod>]
    member _.APathUnderAMissingFolderKeepsTheRest() =
        let harness = seeded ()

        Assert.AreEqual<string * (string * string * string list) list>(
            ("Directory does not exist : /documnts",
             [ "suggestion", "Did you mean documents?", [ "mkdir documents/x" ] ]),
            Said.failing harness "mkdir documnts/x")

        Assert.AreEqual<string * (string * string * string list) list>(
            ("File does not exist : /documnts/notes.txt",
             [ "suggestion", "Did you mean documents/notes.txt?", [ "read documents/notes.txt" ] ]),
            Said.failing harness "read documnts/notes.txt")

    /// The fix is made where the path was written, not where the same word first appears.
    [<TestMethod>]
    member _.TheFixIsWhereThePathWasWritten() =
        let harness = seeded ()

        Assert.AreEqual<string * (string * string * string list) list>(
            ("File does not exist : /read", [ "suggestion", "Did you mean readme.txt?", [ "read readme.txt" ] ]),
            Said.failing harness "read read")

        Assert.AreEqual<string * (string * string * string list) list>(
            ("File does not exist : /notes",
             [ "suggestion", "Did you mean documents/notes.txt?", [ "echo documents/notes.txt | read" ] ]),
            Said.failing harness "echo notes | read")

    [<TestMethod>]
    member _.AtMostThreeAreNamed() =
        let harness = seeded ()

        for name in [ "draft-a"; "draft-b"; "draft-c"; "draft-d" ] do
            harness.Run(sprintf "write %s.txt x" name) |> ignore

        Assert.AreEqual<string * (string * string * string list) list>(
            ("File does not exist : /draft",
             [ "suggestion",
               "Did you mean draft-a.txt, draft-b.txt or draft-c.txt?",
               [ "read draft-a.txt"; "read draft-b.txt"; "read draft-c.txt" ] ]),
            Said.failing harness "read draft")

    /// A mistake inside a script is suggested, and has no place in the line that ran it,
    /// however deep the script was run.
    [<TestMethod>]
    member _.AMistakeInAScriptIsSuggestedWithNoFix() =
        let harness = seeded ()
        harness.Run "write s.clr \"read notes\"" |> ignore
        harness.Run "write u.clr \"run s.clr\"" |> ignore
        harness.Run "write t.clr lss" |> ignore

        Assert.AreEqual<string * (string * string * string list) list>(
            ("/s.clr line 1: File does not exist : /notes",
             [ "suggestion", "Did you mean documents/notes.txt?", [] ]),
            Said.failing harness "run s.clr")

        Assert.AreEqual<string * (string * string * string list) list>(
            ("/u.clr line 1: /s.clr line 1: File does not exist : /notes",
             [ "suggestion", "Did you mean documents/notes.txt?", [] ]),
            Said.failing harness "run u.clr")

        Assert.AreEqual<string * (string * string * string list) list>(
            ("/t.clr line 1: Unknown command : lss", [ "suggestion", "Did you mean ls?", [] ]),
            Said.failing harness "run t.clr")

    /// A nested pipeline is part of the line, so its mistake has a place in it.
    [<TestMethod>]
    member _.AMistakeInANestedPipelineIsFixedInPlace() =
        let harness = seeded ()

        Assert.AreEqual<string * (string * string * string list) list>(
            ("File does not exist : /notes",
             [ "suggestion", "Did you mean documents/notes.txt?", [ "echo (read documents/notes.txt)" ] ]),
            Said.failing harness "echo (read notes)")

    /// `try` makes the fault a value, which carries no notes, and the line that
    /// succeeded has nothing to say (decision 0041).
    [<TestMethod>]
    member _.AFaultTryHoldsHasNoNotes() =
        let harness = seeded ()
        let response = harness.Respond "try read notes | set problem"

        Assert.IsTrue(response.Fault.IsNone)
        Assert.AreEqual<Note list>([], response.Notes)

        match harness.Variable "problem" with
        | Some(Value.Fault fault) ->
            Assert.AreEqual<string>("File does not exist : /notes", fault.Message)
            Assert.AreEqual<Note list>([], fault.Notes)
        | other -> Assert.Fail(sprintf "Expected a fault, got %A." other)

        Assert.AreEqual<string>("File does not exist : /notes", harness.Text "echo $problem.message")
        Assert.AreEqual<Note list>([], (harness.Respond "try lss | set other").Notes)

    /// A live listing's refresh says what the line would.
    [<TestMethod>]
    member _.ARefreshNamesTheNearestToo() =
        let harness = seeded ()
        let response = harness.Refresh "ls documnts"

        Assert.AreEqual<(string * string * string list) list>(
            [ "suggestion", "Did you mean documents?", [ "ls documents" ] ],
            Said.notes response)

/// <summary>An unknown variable names the nearest variables, with a fix for each (0042, 0044).</summary>
[<TestClass>]
type VariableSuggestionTests() =

    [<TestMethod>]
    member _.AnUnknownVariableNamesTheNearest() =
        let harness = seeded ()
        harness.Run "ls | set files" |> ignore

        Assert.AreEqual<string * (string * string * string list) list>(
            ("Unknown variable: $fles", [ "suggestion", "Did you mean $files?", [ "echo $files" ] ]),
            Said.failing harness "echo $fles")

    /// The start of a name, another case, and a member read off it are all corrected.
    [<TestMethod>]
    member _.AStartOrAnotherCaseIsNear() =
        let harness = seeded ()
        harness.Run "ls | set files" |> ignore

        Assert.AreEqual<string * (string * string * string list) list>(
            ("Unknown variable: $fil", [ "suggestion", "Did you mean $files?", [ "echo $files" ] ]),
            Said.failing harness "echo $fil")

        Assert.AreEqual<string * (string * string * string list) list>(
            ("Unknown variable: $Files", [ "suggestion", "Did you mean $files?", [ "echo $files" ] ]),
            Said.failing harness "echo $Files")

        Assert.AreEqual<string * (string * string * string list) list>(
            ("Unknown variable: $fles", [ "suggestion", "Did you mean $files?", [ "echo $files.name" ] ]),
            Said.failing harness "echo $fles.name")

    [<TestMethod>]
    member _.AVariableNothingIsNearSaysNothingMore() =
        let harness = seeded ()
        harness.Run "ls | set files" |> ignore

        Assert.AreEqual<string * (string * string * string list) list>(
            ("Unknown variable: $zebra", []),
            Said.failing harness "echo $zebra")

    /// `$row` is not a variable anyone sets, so its fault says where it exists and
    /// names no variable.
    [<TestMethod>]
    member _.RowIsNeverSuggestedAbout() =
        let harness = seeded ()
        harness.Run "set rows 1" |> ignore

        let _, notes = Said.failing harness "echo $row"
        Assert.AreEqual<(string * string * string list) list>([], notes)
