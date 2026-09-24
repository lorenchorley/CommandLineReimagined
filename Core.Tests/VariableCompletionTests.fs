namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// Variables, their members and tags (stream C).
[<TestClass>]
type VariableCompletionTests() =

    let complete (harness: Harness) (line: string) =
        Async.RunSynchronously(harness.Session.Complete(line, line.Length)).Items

    let texts (harness: Harness) (line: string) = complete harness line |> List.map (fun c -> c.Text)

    let details (harness: Harness) (line: string) =
        complete harness line |> List.map (fun c -> c.Text, defaultArg c.Detail "")

    let detailOf (harness: Harness) (line: string) (text: string) =
        match complete harness line |> List.tryFind (fun c -> c.Text = text) with
        | Some completion -> defaultArg completion.Detail ""
        | None -> failwithf "'%s' offered no %s: %A" line text (texts harness line)

    /// The Acceptance table's session: `set v 5`, `ls | set files`, `try read missing.txt | set problem`.
    let accepted () =
        let harness = seeded ()
        harness.Run "set v 5" |> ignore
        harness.Run "ls | set files" |> ignore
        harness.Run "try read missing.txt | set problem" |> ignore
        harness

    /// The smoke test the foundation leaves: the stream fills in the rest.
    [<TestMethod>]
    member _.AVariableCompletes() =
        let harness = seeded ()

        assertContains "$row" (texts harness "ls | where $r")

    // ------------------------------------------------------------ Summaries (finding 1)

    [<TestMethod>]
    member _.ASummaryNamesTheKindAndWhatTheValueCarries() =
        let file = { Id = "f"; Name = "readme.txt"; Kind = "text"; Folder = "/" }

        let cases =
            [ Value.Number 5.0, "number · 5"
              Value.Boolean true, "boolean · true"
              Value.Text "hello", "text · \"hello\""
              Value.File file, "file · readme.txt · text"
              Value.File { file with Name = "documents"; Kind = Value.folderKind }, "folder · documents"
              Value.Object(Tag.create "note" [ "name", Value.Text "monday"; "mood", Value.Text "good" ] []),
              "tag · note · 2 attributes"
              Value.Object(Tag.create "note" [ "name", Value.Text "monday" ] []), "tag · note · 1 attribute"
              Value.Fault(Fault.create NotFound "Could not find missing.txt"),
              "fault · NotFound · Could not find missing.txt"
              Value.Query(Expr.Compare("eq", Expr.Variable("row", [ "kind" ]), Expr.Const(Value.Text "folder"))),
              "query · $row.kind eq folder"
              Value.List [ Value.Number 1.0; Value.Number 2.0; Value.Number 3.0 ], "list · 3 items"
              Value.List [ Value.Number 1.0 ], "list · 1 item"
              Value.Table(Table.ofColumns [ "name"; "value" ] [ [ Value.Text "a"; Value.Number 1.0 ] ]),
              "table · 1 row · name, value"
              Value.None, "none" ]

        for value, expected in cases do
            Assert.AreEqual<string>(expected, Summary.ofValue value)

    /// A table names its first three columns and says there are more.
    [<TestMethod>]
    member _.ATableSummaryCountsItsRowsAndNamesItsColumns() =
        let harness = accepted ()
        let rows = (harness.Table "ls").Rows.Length

        Assert.AreEqual<string>(
            sprintf "table · %d rows · name, kind, folder…" rows,
            Summary.ofValue (harness.Variable "files" |> Option.get)
        )

    /// A long text or message is cut to one short line, and a line break folded.
    [<TestMethod>]
    member _.ALongTextIsCutShort() =
        let summary = Summary.ofValue (Value.Text(String.replicate 100 "a"))

        StringAssert.StartsWith(summary, "text · \"aaaa")
        StringAssert.EndsWith(summary, "…\"")
        Assert.IsTrue(summary.Length < 60, summary)
        Assert.AreEqual<string>("text · \"one two\"", Summary.ofValue (Value.Text "one\ntwo"))

    /// A size is not something a file value carries: it takes a read of the content,
    /// so it is only there when the caller measured it.
    [<TestMethod>]
    member _.AFileSizeIsGivenWhenTheCallerKnowsIt() =
        let file = { Id = "f"; Name = "readme.txt"; Kind = "text"; Folder = "/" }

        Assert.AreEqual<string>("file · readme.txt · text · 41 bytes", Summary.ofValueWith (fun _ -> Some 41.0) (Value.File file))

    /// Finding 1: each chip says what it holds.
    [<TestMethod>]
    member _.EachVariableChipCarriesItsSummary() =
        let harness = accepted ()
        let offered = details harness "$"

        Assert.AreEqual<string list>([ "$files"; "$problem"; "$v" ], offered |> List.map fst)
        Assert.AreEqual<string>("number · 5", detailOf harness "$" "$v")
        StringAssert.StartsWith(detailOf harness "$" "$files", "table · ")
        StringAssert.StartsWith(detailOf harness "$" "$problem", "fault · NotFound · ")

        for text, detail in offered do
            Assert.AreEqual<string>(
                Summary.ofValue (harness.Variable(text.Substring 1) |> Option.get),
                detail,
                sprintf "The chip for %s should carry its summary." text
            )

    [<TestMethod>]
    member _.AVariableChipReplacesTheWholeWord() =
        let harness = accepted ()
        let line = "echo $fi"

        match complete harness line with
        | [ completion ] ->
            Assert.AreEqual<string>("$files", completion.Text)
            Assert.AreEqual<int>(5, completion.Start)
            Assert.AreEqual<int>(line.Length, completion.End)
            Assert.AreEqual<string>("variable", completion.Kind)
        | other -> Assert.Fail(sprintf "Expected $files alone, got %A" other)

        // A variable tag keeps its sigil.
        Assert.AreEqual<string list>([ "<$files" ], texts harness "echo <$fi")

    // ------------------------------------------------------------ $row (finding 2)

    /// Finding 2: `$row` exists only inside a predicate, so it is not offered outside one.
    [<TestMethod>]
    member _.RowIsNotOfferedOutsideAPredicate() =
        let harness = accepted ()

        for line in [ "$"; "echo $"; "echo $r"; "$r" ] do
            assertDoesNotContain "$row" (texts harness line)

    /// Inside a predicate it is offered first, and says what it is.
    [<TestMethod>]
    member _.RowIsOfferedFirstInsideAPredicate() =
        let harness = accepted ()

        for line in [ "ls | where $"; "ls | where $row.kind eq $" ] do
            match details harness line with
            | (text, detail) :: rest ->
                Assert.AreEqual<string>("$row", text)
                Assert.AreEqual<string>("the row being tested", detail)
                Assert.AreEqual<string list>([ "$files"; "$problem"; "$v" ], rest |> List.map fst)
            | [] -> Assert.Fail(sprintf "'%s' offered nothing." line)

        Assert.AreEqual<string list>([ "$row" ], texts harness "ls | where $r")
        Assert.AreEqual<string list>([ "$v" ], texts harness "ls | where $v")

    // ------------------------------------------------------------ Members (finding 5)

    /// Finding 5: a number has no members, so there is nothing to offer.
    [<TestMethod>]
    member _.ANumberHasNoMembers() =
        let harness = accepted ()
        harness.Run "set t hello" |> ignore
        harness.Run "set b true" |> ignore

        for line in [ "$v."; "echo $v."; "$t."; "$b."; "$nothing." ] do
            Assert.AreEqual<int>(0, (texts harness line).Length, line)

    /// A fault can be asked what it was (decision 0014).
    [<TestMethod>]
    member _.AFaultsMembersAreWhatItCanBeAsked() =
        let harness = accepted ()

        Assert.AreEqual<string list>(
            [ "$problem.kind"; "$problem.message"; "$problem.stage"; "$problem.path" ],
            texts harness "$problem."
        )

        Assert.AreEqual<string>("text · \"NotFound\"", detailOf harness "$problem." "$problem.kind")
        Assert.AreEqual<string list>([ "$problem.kind" ], texts harness "echo $problem.ki")

    /// A table has no members to offer: a column is read off a row, and `$files.name`
    /// answers nothing, so offering it offered nothing real. `$row.` inside a predicate
    /// still offers the table's columns, from what flows into the stage.
    [<TestMethod>]
    member _.ATableOffersNoMembers() =
        let harness = accepted ()

        for line in [ "$files."; "echo $files."; "$files.na" ] do
            Assert.AreEqual<int>(0, (texts harness line).Length, line)

        Assert.AreEqual<string>("", harness.Text "echo $files.name")

        CollectionAssert.IsSubsetOf(
            [| "$row.name"; "$row.kind"; "$row.size" |],
            texts harness "$files | where $row." |> Array.ofList)

    /// A tag's members are its attributes, in the order they were written, with what
    /// each holds, then its own parts (decision 0048); a path is followed the way the
    /// evaluator reads members.
    [<TestMethod>]
    member _.ATagsMembersAreItsAttributes() =
        let harness = seeded ()
        harness.Run "set inner <circle radius=1/>" |> ignore
        harness.Run "set shape <square side=2 inner=$inner/>" |> ignore

        Assert.AreEqual<string list>(
            [ "$shape.side"; "$shape.inner"; "$shape.@tag"; "$shape.@children" ],
            texts harness "$shape."
        )

        Assert.AreEqual<string>("number · 2", detailOf harness "$shape." "$shape.side")

        Assert.AreEqual<string list>(
            [ "$shape.inner.radius"; "$shape.inner.@tag"; "$shape.inner.@children" ],
            texts harness "$shape.inner."
        )

        Assert.AreEqual<int>(0, (texts harness "$shape.side.").Length)

    /// The Acceptance line: `$v.` on `<thing a=1/>` offers `a`, `@tag` and `@children`,
    /// the last two saying what they read.
    [<TestMethod>]
    member _.ATagOffersItsOwnPartsAfterItsAttributes() =
        let harness = seeded ()
        harness.Run "set v <thing a=1/>" |> ignore

        Assert.AreEqual<string list>([ "$v.a"; "$v.@tag"; "$v.@children" ], texts harness "$v.")
        Assert.AreEqual<string list>([ "$v.a"; "$v.@tag"; "$v.@children" ], texts harness "echo $v.")
        Assert.AreEqual<string>("the tag's name", detailOf harness "$v." "$v.@tag")
        Assert.AreEqual<string>("its children", detailOf harness "$v." "$v.@children")

    /// Typing the `@` leaves the tag's own parts alone, since no attribute starts with one.
    [<TestMethod>]
    member _.AnAtOffersOnlyTheTagsOwnParts() =
        let harness = seeded ()
        harness.Run "set v <thing a=1 at=2/>" |> ignore

        Assert.AreEqual<string list>([ "$v.@tag"; "$v.@children" ], texts harness "$v.@")
        Assert.AreEqual<string list>([ "$v.@tag" ], texts harness "echo $v.@t")
        Assert.AreEqual<string list>([ "$v.@children" ], texts harness "echo $v.@c")

    /// `$d.@children.` is a list, which has no members; the members of a child are
    /// reached through a variable that holds it.
    [<TestMethod>]
    member _.ChildrenAreAListWithNoMembers() =
        let harness = seeded ()
        harness.Run "set d <a><b/><c/></a>" |> ignore

        Assert.AreEqual<int>(0, (texts harness "$d.@children.").Length)
        Assert.AreEqual<int>(0, (texts harness "$d.@tag.").Length)

    /// A value that is not a tag has no own parts to offer.
    [<TestMethod>]
    member _.OnlyATagOffersItsOwnParts() =
        let harness = accepted ()

        for line in [ "$v.@"; "$files.@"; "$problem.@" ] do
            Assert.AreEqual<int>(0, (texts harness line).Length, line)

    [<TestMethod>]
    member _.AFilesMembersAreWhatAFileCanBeAsked() =
        let harness = seeded ()
        harness.Run "save <note name=monday/> | set f" |> ignore

        Assert.AreEqual<string list>([ "$f.name"; "$f.kind"; "$f.folder"; "$f.path"; "$f.id" ], texts harness "$f.")
        Assert.AreEqual<string>("text · \"monday\"", detailOf harness "$f." "$f.name")
        Assert.AreEqual<string>("file · monday · note", detailOf harness "$" "$f")

    /// `$row.` asks what flows into the stage, and each column says its type.
    [<TestMethod>]
    member _.RowsMembersAreTheColumnsThatFlowIn() =
        let harness = seeded ()
        let offered = details harness "ls | where $row."

        CollectionAssert.IsSubsetOf(
            [| "$row.name"; "$row.kind"; "$row.folder"; "$row.size"; "$row.modified" |],
            offered |> List.map fst |> Array.ofList
        )

        Assert.AreEqual<string>("number", detailOf harness "ls | where $row." "$row.size")
        Assert.AreEqual<string list>([ "$row.kind" ], texts harness "ls | where $row.ki")

    // ------------------------------------------------------------ Tags (finding 21)

    /// After `<`, the types in use: the kinds of the records here, and the types of
    /// the tags variables hold. Folders and views are made otherwise.
    [<TestMethod>]
    member _.AfterAnAngleTheTagTypesInUse() =
        let harness = seeded ()
        harness.Run "save <note name=monday mood=good/>" |> ignore
        harness.Run "set shape <square side=2/>" |> ignore

        let offered = texts harness "save <"

        assertContains "<note" offered
        assertContains "<square" offered
        assertContains "<text" offered
        assertDoesNotContain "<folder" offered
        assertDoesNotContain "<view" offered
        Assert.AreEqual<string list>([ "<note" ], texts harness "save <no")
        Assert.AreEqual<string>("1 record", detailOf harness "save <no" "<note")
        Assert.AreEqual<string>("1 tag in variables", detailOf harness "save <sq" "<square")

    [<TestMethod>]
    member _.ATagTypeReplacesTheWholeWord() =
        let harness = seeded ()
        harness.Run "save <note name=monday/>" |> ignore

        match complete harness "save <no" with
        | [ completion ] ->
            Assert.AreEqual<string>("<note", completion.Text)
            Assert.AreEqual<int>(5, completion.Start)
            Assert.AreEqual<int>(8, completion.End)
        | other -> Assert.Fail(sprintf "Expected <note alone, got %A" other)

    /// After `<note `, the attributes notes carry, `name` first, as `name=`; the
    /// runtime's own are not written in a tag, and one already written is not offered.
    [<TestMethod>]
    member _.InsideATagTheAttributesItsTypeCarries() =
        let harness = seeded ()
        harness.Run "save <note name=monday mood=good/>" |> ignore
        harness.Run "save <note name=tuesday mood=better tag=work/>" |> ignore

        Assert.AreEqual<string list>([ "name="; "mood="; "tag=" ], texts harness "save <note ")
        Assert.AreEqual<string>("2 of 2 carry it", detailOf harness "save <note " "mood=")
        Assert.AreEqual<string>("1 of 2 carry it", detailOf harness "save <note " "tag=")
        Assert.AreEqual<string list>([ "name="; "tag=" ], texts harness "save <note mood=good ")
        Assert.AreEqual<string list>([ "mood=" ], texts harness "save <note name=\"a b\" m")
        Assert.AreEqual<int>(0, (texts harness "save <unheard ").Length)
