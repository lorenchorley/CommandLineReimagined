namespace CommandLineReimagined.Core.Tests

open System.Threading
open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>Arguments by what their parameter takes, and the signature (stream D).</summary>
/// <remarks>
/// Findings 12 to 20 of Phase 8. `‸` marks the cursor where it is not at the end.
/// </remarks>
[<TestClass>]
type ArgumentCompletionTests() =

    static let cursorOf (line: string) =
        let cursor = line.IndexOf '‸'
        let text = line.Replace("‸", "")
        text, (if cursor < 0 then text.Length else cursor)

    static let result (harness: Harness) (line: string) =
        let text, cursor = cursorOf line
        Async.RunSynchronously(harness.Session.Complete(text, cursor))

    static let items (harness: Harness) (line: string) = (result harness line).Items

    static let texts (harness: Harness) (line: string) = items harness line |> List.map (fun c -> c.Text)

    static let signature (harness: Harness) (line: string) = (result harness line).Signature

    /// <summary>What every parameter of every command takes.</summary>
    /// <remarks>
    /// A new command, or a new parameter, has no row here and fails
    /// `EveryParameterSaysWhatItTakes`, rather than quietly being offered files. A
    /// `Predicate` parameter and the `Assignments` collector keep `Anything`: their kind
    /// already sends the word elsewhere (a predicate to stream E's provider, an
    /// assignment to the record's attributes), except `in`'s, which also takes a place.
    /// </remarks>
    static let expected: ((string * string) * Takes) list =
        [ ("attr", "path"), Takes.Path
          ("attr", "assignments"), Takes.Anything
          ("read", "path"), Takes.Path
          ("in", "TargetPath"), Takes.Place
          ("columns", "table"), Takes.Value
          ("count", "table"), Takes.Value
          ("cp", "sourcePathAndFile"), Takes.Path
          ("cp", "targetPath"), Takes.Place
          ("distinct", "column"), Takes.Column
          ("distinct", "table"), Takes.Value
          ("download", "url"), Takes.Url
          ("download", "into"), Takes.Place
          ("echo", "text"), Takes.Value
          ("find", "predicate"), Takes.Anything
          ("first", "table"), Takes.Value
          ("from-csv", "path"), Takes.Path
          ("from-csv", "delimiter"), Takes.Text
          ("from-xml", "path"), Takes.Path
          ("group", "column"), Takes.Column
          ("group", "table"), Takes.Value
          // Stream A's: `help` takes a command once A merges.
          ("help", "command"), Takes.CommandName
          ("is-fault", "value"), Takes.Value
          ("last", "table"), Takes.Value
          ("ls", "path"), Takes.Place
          ("mkdir", "FolderName"), Takes.NewName
          ("progress", "steps"), Takes.Count
          ("progress", "delay"), Takes.Number
          ("pick", "selector"), Takes.Selector
          ("pick", "document"), Takes.Value
          ("rm", "path"), Takes.Path
          ("rows", "table"), Takes.Value
          ("run", "path"), Takes.Path
          ("save", "tag"), Takes.Value
          ("save-view", "name"), Takes.NewName
          ("save-view", "predicate"), Takes.Anything
          ("select", "columns"), Takes.Column
          ("select", "table"), Takes.Value
          ("set", "name"), Takes.VariableName
          ("set", "value"), Takes.Value
          ("skip", "count"), Takes.Count
          ("skip", "table"), Takes.Value
          ("sort", "column"), Takes.Column
          ("sort", "desc"), Takes.Switch("desc", Some "asc")
          ("sort", "table"), Takes.Value
          ("table", "table"), Takes.Value
          ("take", "count"), Takes.Count
          ("take", "table"), Takes.Value
          ("to-csv", "path"), Takes.Path
          ("to-csv", "value"), Takes.Value
          ("to-csv", "delimiter"), Takes.Text
          ("to-xml", "path"), Takes.Path
          ("to-xml", "value"), Takes.Value
          ("to-xml", "root"), Takes.Text
          ("to-xml", "row"), Takes.Text
          ("to-xml", "declaration"), Takes.Switch("declaration", None)
          ("where", "predicate"), Takes.Anything
          ("where", "table"), Takes.Value
          ("write", "path"), Takes.Path
          ("write", "text"), Takes.Value ]

    /// Rows another stream's parameter will fill, which may not exist yet.
    static let pending = set [ "help", "command" ]

    // ------------------------------------------------------- every parameter

    [<TestMethod>]
    member _.EveryParameterSaysWhatItTakes() =
        let table = Map.ofList expected
        let specs = (seeded ()).Session.Commands

        let wrong =
            [ for spec in specs do
                  for parameter in spec.Parameters do
                      match Map.tryFind (spec.Name, parameter.Name) table with
                      | None -> yield sprintf "%s %s has no row: say what it takes" spec.Name parameter.Name
                      | Some takes when takes <> parameter.Takes ->
                          yield sprintf "%s %s takes %A, the table says %A" spec.Name parameter.Name parameter.Takes takes
                      | Some _ -> () ]

        let declared =
            specs |> List.collect (fun spec -> spec.Parameters |> List.map (fun p -> spec.Name, p.Name)) |> Set.ofList

        let stale =
            expected
            |> List.map fst
            |> List.filter (fun key -> not (Set.contains key declared) && not (Set.contains key pending))
            |> List.map (fun (command, parameter) -> sprintf "%s %s is in the table but not declared" command parameter)

        match wrong @ stale with
        | [] -> ()
        | problems -> Assert.Fail("\n" + String.concat "\n" problems)

    /// Each parameter, at a place where the word binds to it, reads as that parameter.
    /// A table's `table`, a flag's value and the collector are reached another way.
    [<TestMethod>]
    member _.EveryParameterIsReachedWhereItsWordIs() =
        let lines =
            [ "attr ", "Argument(attr, path)"
              "read ", "Argument(read, path)"
              "cp ", "Argument(cp, sourcePathAndFile)"
              "cp a ", "Argument(cp, targetPath)"
              "distinct ", "Argument(distinct, column)"
              "download ", "Argument(download, url)"
              "download u ", "Argument(download, into)"
              "echo ", "Argument(echo, text)"
              "from-csv ", "Argument(from-csv, path)"
              "from-csv a ", "Argument(from-csv, delimiter)"
              "from-xml ", "Argument(from-xml, path)"
              "group ", "Argument(group, column)"
              "is-fault ", "Argument(is-fault, value)"
              "ls ", "Argument(ls, path)"
              "mkdir ", "Argument(mkdir, FolderName)"
              "progress ", "Argument(progress, steps)"
              "progress 5 ", "Argument(progress, delay)"
              "rm ", "Argument(rm, path)"
              "run ", "Argument(run, path)"
              "save ", "Argument(save, tag)"
              "save-view ", "Argument(save-view, name)"
              "select ", "Argument(select, columns)"
              "set ", "Argument(set, name)"
              "set v ", "Argument(set, value)"
              "skip ", "Argument(skip, count)"
              "sort ", "Argument(sort, column)"
              "sort name ", "Argument(sort, desc)"
              "sort name desc ", "Argument(sort, table)"
              "take ", "Argument(take, count)"
              "to-csv ", "Argument(to-csv, path)"
              "to-csv a b ", "Argument(to-csv, delimiter)"
              "to-xml a b ", "Argument(to-xml, root)"
              "write ", "Argument(write, path)"
              "write a ", "Argument(write, text)" ]

        let specs = (seeded ()).Session.Commands

        let wrong =
            lines
            |> List.choose (fun (line, place) ->
                let actual = Context.describe (Context.analyse specs line line.Length).Place
                if actual = place then None else Some(sprintf "%-20s expected %s, read %s" line place actual))

        if not (List.isEmpty wrong) then
            Assert.Fail("\n" + String.concat "\n" wrong)

    // ------------------------------------------------------- 12: columns

    [<TestMethod>]
    member _.AColumnOffersTheColumnsThatFlowIn() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "name"; "kind"; "folder"; "size"; "modified" ], texts harness "ls | sort ")

        for line in [ "select "; "ls | select "; "group "; "ls | group " ] do
            Assert.AreEqual<string list>([ "name"; "kind"; "folder"; "size"; "modified" ], texts harness line, line)

        // `distinct`'s column is optional, so the stage is complete and the pipe comes
        // first (decision 0039).
        for line in [ "distinct "; "ls | distinct " ] do
            Assert.AreEqual<string list>([ "|"; "name"; "kind"; "folder"; "size"; "modified" ], texts harness line, line)

    [<TestMethod>]
    member _.AColumnSaysItsType() =
        let harness = seeded ()
        let offered = items harness "ls | sort "

        Assert.IsTrue(offered |> List.forall (fun c -> c.Kind = "column"))

        let detail name =
            (offered |> List.find (fun c -> c.Text = name)).Detail

        // A listing's name column holds the record itself.
        Assert.AreEqual<string option>(Some "file", detail "name")
        Assert.AreEqual<string option>(Some "text", detail "kind")
        Assert.AreEqual<string option>(Some "number", detail "size")

    [<TestMethod>]
    member _.AColumnCompletesByItsPrefix() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "kind" ], texts harness "ls | sort k")
        Assert.AreEqual<string list>([ "size" ], texts harness "ls | sort S")

    /// A listing's columns include every attribute something here carries.
    [<TestMethod>]
    member _.AColumnIncludesTheAttributesThingsHereCarry() =
        let harness = seeded ()
        harness.Run "attr readme.txt mood=good" |> ignore

        assertContains "mood" (texts harness "ls | sort ")

    /// `select name name` asks for nothing more, so a column already written is not
    /// offered again.
    [<TestMethod>]
    member _.ARestOfColumnsLeavesOutTheOnesWritten() =
        let harness = seeded ()
        let offered = texts harness "ls | select name "

        assertDoesNotContain "name" offered
        assertContains "kind" offered

    [<TestMethod>]
    member _.AColumnIsNeverAFile() =
        let harness = seeded ()

        assertDoesNotContain "readme.txt" (texts harness "ls | sort ")
        assertDoesNotContain "documents/" (texts harness "ls | group ")

    // ------------------------------------------------------- 13: switches

    [<TestMethod>]
    member _.ASwitchOffersItsWords() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "desc" ], texts harness "ls | sort name d")
        Assert.AreEqual<string list>([ "asc" ], texts harness "ls | sort name a")
        Assert.AreEqual<string list>([ "|"; "desc"; "asc" ], texts harness "ls | sort name ")

    [<TestMethod>]
    member _.ASwitchWordIsAKeywordWithWhatItDoes() =
        let harness = seeded ()

        match items harness "ls | sort name d" with
        | [ desc ] ->
            Assert.AreEqual<string>("keyword", desc.Kind)
            Assert.AreEqual<string option>(Some "Write 'desc' to order downwards", desc.Detail)
        | other -> Assert.Fail(sprintf "%A" other)

    // ------------------------------------------------------- 14: nothing to pick

    [<TestMethod>]
    member _.ACountOffersNothing() =
        let harness = seeded ()

        for line in [ "ls | take "; "ls | skip "; "take " ] do
            Assert.AreEqual<string list>([], texts harness line, line)

        // `progress`'s numbers are both optional: only the pipe (decision 0039).
        for line in [ "progress "; "progress 5 " ] do
            Assert.AreEqual<string list>([ "|" ], texts harness line, line)

    [<TestMethod>]
    member _.ANewNameATextAndAUrlOfferNothing() =
        let harness = seeded ()

        for line in [ "mkdir "; "mkdir d"; "save-view " ] do
            Assert.AreEqual<string list>([], texts harness line, line)

        // Optional, so the stage is complete: only the pipe (decision 0039).
        for line in [ "download "; "from-csv readme.txt " ] do
            Assert.AreEqual<string list>([ "|" ], texts harness line, line)

    /// Nothing to pick is where the signature earns its keep: it says what is wanted.
    [<TestMethod>]
    member _.ACountIsNamedByTheSignature() =
        let harness = seeded ()

        match signature harness "ls | take " with
        | Some signature ->
            Assert.AreEqual<string>("take", signature.Command)
            Assert.AreEqual<int option>(Some 0, signature.Active)
            let name, optional, _ = signature.Parameters[0]
            Assert.AreEqual<string>("count", name)
            Assert.IsFalse optional
        | None -> Assert.Fail "no signature"

        Assert.AreEqual<int option>(Some 1, (signature harness "progress 5 ").Value.Active)

    // ------------------------------------------------------- 15: variable names

    [<TestMethod>]
    member _.AVariableNameOffersTheVariablesThereAre() =
        let harness = seeded ()
        harness.Run "set v 5" |> ignore
        harness.Run "ls | set files" |> ignore

        Assert.AreEqual<string list>([ "files"; "v" ], texts harness "set ")
        Assert.AreEqual<string list>([ "files" ], texts harness "set f")

    [<TestMethod>]
    member _.AVariableNameSaysWhatItHolds() =
        let harness = seeded ()
        harness.Run "set v 5" |> ignore

        match items harness "set " with
        | [ v ] ->
            Assert.AreEqual<string>("variable", v.Kind)
            Assert.AreEqual<string option>(Some(Summary.ofValue (Value.Number 5.0)), v.Detail)
        | other -> Assert.Fail(sprintf "%A" other)

    [<TestMethod>]
    member _.AVariableNameIsNeverAFile() =
        let harness = seeded ()

        Assert.AreEqual<string list>([], texts harness "set ")
        Assert.AreEqual<string list>([], texts harness "set re")

    /// Where any value can go, the variables are the things worth offering.
    [<TestMethod>]
    member _.AValueOffersTheVariablesInScope() =
        let harness = seeded ()
        harness.Run "set v 5" |> ignore

        Assert.AreEqual<string list>([ "$v" ], texts harness "set x ")
        Assert.AreEqual<string list>([ "$v" ], texts harness "echo ")
        Assert.AreEqual<string list>([ "$v" ], texts harness "echo v")
        // The table is optional, and nothing feeds it at the head of a line.
        Assert.AreEqual<string list>([ "|"; "$v" ], texts harness "sort name desc ")
        Assert.AreEqual<string option>(Some(Summary.ofValue (Value.Number 5.0)), (items harness "echo ").Head.Detail)

    // ------------------------------------------------------- 16: predicates

    /// What a predicate's word could be is stream E's, but the signature is here: the
    /// predicate is the parameter being written.
    [<TestMethod>]
    member _.APredicateIsMarkedInTheSignature() =
        let harness = seeded ()

        for line, command, active in
            [ "where ", "where", 0
              "ls | where $row.kind eq ", "where", 0
              "find ", "find", 0
              "save-view x ", "save-view", 1
              "in ", "in", 0 ] do
            match signature harness line with
            | Some signature ->
                Assert.AreEqual<string>(command, signature.Command, line)
                Assert.AreEqual<int option>(Some active, signature.Active, line)
            | None -> Assert.Fail(sprintf "%s has no signature" line)

    /// The name of a view does not exist yet, so there is nothing to offer for it.
    [<TestMethod>]
    member _.AViewsNameOffersNothing() =
        let harness = seeded ()

        Assert.AreEqual<string list>([], texts harness "save-view ")
        Assert.AreEqual<int option>(Some 0, (signature harness "save-view ").Value.Active)

    // ------------------------------------------------------- 17: attributes

    [<TestMethod>]
    member _.AnAssignmentOffersTheRecordsAttributes() =
        let harness = seeded ()

        // `attr readme.txt` alone answers its attributes, so the pipe comes first.
        Assert.AreEqual<string list>([ "|"; "name="; "kind=" ], texts harness "attr readme.txt ")

    [<TestMethod>]
    member _.AnAssignmentOffersAttributesAUserSet() =
        let harness = seeded ()
        harness.Run "attr readme.txt mood=good" |> ignore

        Assert.AreEqual<string list>([ "|"; "mood="; "name="; "kind=" ], texts harness "attr readme.txt ")
        Assert.AreEqual<string list>([ "mood=" ], texts harness "attr readme.txt mo")

        match items harness "attr readme.txt mo" with
        | [ mood ] ->
            Assert.AreEqual<string>("member", mood.Kind)
            Assert.AreEqual<string option>(Some(Summary.ofValue (Value.Text "good")), mood.Detail)
        | other -> Assert.Fail(sprintf "%A" other)

    [<TestMethod>]
    member _.AnAssignmentLeavesOutTheOnesWrittenAndTheTerminals() =
        let harness = seeded ()
        let offered = texts harness "attr documents/notes.txt name=x "

        Assert.AreEqual<string list>([ "|"; "kind=" ], offered)

        for owned in [ "folder="; "created="; "modified="; "size=" ] do
            assertDoesNotContain owned (texts harness "attr readme.txt ")

    [<TestMethod>]
    member _.AnAssignmentToNothingOffersNothing() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "|" ], texts harness "attr missing.txt ")

    [<TestMethod>]
    member _.AnAssignmentIsMarkedInTheSignature() =
        let harness = seeded ()

        Assert.AreEqual<int option>(Some 1, (signature harness "attr readme.txt ").Value.Active)
        Assert.AreEqual<int option>(Some 1, (signature harness "attr readme.txt mood=go").Value.Active)
        Assert.AreEqual<int option>(Some 0, (signature harness "attr ").Value.Active)

    // ------------------------------------------------------- 18: flags

    [<TestMethod>]
    member _.AFlagOffersTheCommandsFlags() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "-desc" ], texts harness "ls | sort name -")
        Assert.AreEqual<string list>([ "-desc" ], texts harness "ls | sort name -de")
        Assert.AreEqual<string list>([ "-steps"; "-delay" ], texts harness "progress -")
        Assert.AreEqual<string list>([ "-root"; "-row"; "-declaration" ], texts harness "to-xml a.xml -")

    [<TestMethod>]
    member _.AFlagSaysWhatItIsFor() =
        let harness = seeded ()

        match items harness "ls | sort name -" with
        | [ flag ] ->
            Assert.AreEqual<string>("flag", flag.Kind)
            Assert.AreEqual<string option>(Some "Write 'desc' to order downwards", flag.Detail)
            Assert.AreEqual<int>(15, flag.Start)
        | other -> Assert.Fail(sprintf "%A" other)

    [<TestMethod>]
    member _.AFlagAlreadyWrittenIsNotOfferedAgain() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "-delay" ], texts harness "progress -steps 5 -")

    [<TestMethod>]
    member _.AFlagOfAnUnknownCommandOffersNothing() =
        let harness = seeded ()

        Assert.AreEqual<string list>([], texts harness "frob -")

    [<TestMethod>]
    member _.AFlagWrittenOutIsMarkedInTheSignature() =
        let harness = seeded ()

        Assert.AreEqual<int option>(Some 1, (signature harness "ls | sort name -desc").Value.Active)
        Assert.AreEqual<int option>(None, (signature harness "ls | sort name -").Value.Active)

    // ------------------------------------------------------- 19: the signature

    [<TestMethod>]
    member _.AnArgumentHasItsCommandsSignature() =
        let harness = seeded ()

        match signature harness "ls | sort " with
        | Some signature ->
            Assert.AreEqual<string>("sort", signature.Command)
            Assert.AreEqual<string>("Order the rows by a column", signature.Description)

            Assert.AreEqual<(string * bool) list>(
                [ "column", false; "desc", true; "table", true ],
                signature.Parameters |> List.map (fun (name, optional, _) -> name, optional)
            )

            Assert.AreEqual<string>("The column to order by", (let _, _, d = signature.Parameters[0] in d))
            Assert.AreEqual<int option>(Some 0, signature.Active)
        | None -> Assert.Fail "no signature"

    [<TestMethod>]
    member _.TheActiveParameterIsTheOneTheWordBindsTo() =
        let harness = seeded ()
        let active line = (signature harness line).Value.Active

        Assert.AreEqual<int option>(Some 0, active "ls | sort na")
        Assert.AreEqual<int option>(Some 1, active "ls | sort name d")
        Assert.AreEqual<int option>(Some 2, active "sort name desc ")
        Assert.AreEqual<int option>(Some 0, active "ls | select name ")
        Assert.AreEqual<int option>(Some 1, active "cp readme.txt ")
        Assert.AreEqual<int option>(Some 0, active "ls | sort ‸ | take 2")
        Assert.AreEqual<int option>(Some 0, active "read \"doc")

    /// More than the command takes binds to nothing, so nothing is marked; the
    /// signature is still shown, to say what the command does take.
    [<TestMethod>]
    member _.ASurplusWordMarksNothing() =
        let harness = seeded ()

        match signature harness "read x y" with
        | Some signature ->
            Assert.AreEqual<string>("read", signature.Command)
            Assert.AreEqual<int option>(None, signature.Active)
        | None -> Assert.Fail "no signature"

    [<TestMethod>]
    member _.OnlyAnArgumentOrAPredicateHasASignature() =
        let harness = seeded ()
        harness.Run "set v 5" |> ignore

        for line in [ ""; "wh"; "ls | "; "$"; "echo $v"; "$v."; "save <"; "save <note "; "frob x"; "ls | where $row." ] do
            Assert.AreEqual<Signature option>(None, signature harness line, line)

    /// A command with no parameters has a signature with none in it, when a word is
    /// written after it anyway.
    [<TestMethod>]
    member _.ACommandWithNoParametersHasAnEmptySignature() =
        let harness = seeded ()

        match signature harness "pwd x" with
        | Some signature ->
            Assert.AreEqual<int>(0, signature.Parameters.Length)
            Assert.AreEqual<int option>(None, signature.Active)
        | None -> Assert.Fail "no signature"

    // ------------------------------------------------------- 20: paths, and quoted ones

    [<TestMethod>]
    member _.AnArgumentCompletes() =
        let harness = seeded ()

        assertContains "readme.txt" (texts harness "read re")

    [<TestMethod>]
    member _.AQuotedWordCompletesQuoted() =
        let harness = seeded ()

        match items harness "read \"doc" with
        | [ folder ] ->
            Assert.AreEqual<string>("\"documents/\"", folder.Text)
            Assert.AreEqual<string>("folder", folder.Kind)
            Assert.AreEqual<int>(5, folder.Start)
            Assert.AreEqual<int>(9, folder.End)
        | other -> Assert.Fail(sprintf "%A" other)

        Assert.AreEqual<string list>([ "\"documents/notes.txt\"" ], texts harness "read \"documents/no")
        Assert.AreEqual<string list>([ "\"readme.txt\"" ], texts harness "read \"re")

    /// A closing quote already there is part of the word, so it is replaced rather than
    /// doubled.
    [<TestMethod>]
    member _.AQuotedWordReplacesItsClosingQuote() =
        let harness = seeded ()

        match items harness "read \"re‸\" | count" with
        | [ file ] ->
            Assert.AreEqual<string>("\"readme.txt\"", file.Text)
            Assert.AreEqual<int>(5, file.Start)
            Assert.AreEqual<int>(9, file.End)
        | other -> Assert.Fail(sprintf "%A" other)

    [<TestMethod>]
    member _.ANameWithASpaceCompletesQuoted() =
        let harness = seeded ()
        harness.Run "write \"my notes.txt\" hello" |> ignore

        Assert.AreEqual<string list>([ "\"my notes.txt\"" ], texts harness "read my")
        Assert.AreEqual<string list>([ "\"my notes.txt\"" ], texts harness "read \"my n")

        // And what it completes to is a line that runs.
        Assert.AreEqual<string>("hello", harness.Text "read \"my notes.txt\"")

    [<TestMethod>]
    member _.AQuotedWordIsNeverTheKeyword() =
        let harness = seeded ()

        assertDoesNotContain "else" (texts harness "read x \"el")

    /// `read re| documents`: the completion replaces the word and keeps what follows.
    [<TestMethod>]
    member _.APathMidLineKeepsTheRestOfTheLine() =
        let harness = seeded ()

        match items harness "read re‸ documents" with
        | [ file ] ->
            Assert.AreEqual<string>("readme.txt", file.Text)
            Assert.AreEqual<int>(5, file.Start)
            Assert.AreEqual<int>(7, file.End)
        | other -> Assert.Fail(sprintf "%A" other)

    [<TestMethod>]
    member _.APlaceOffersFoldersAndViewsOnly() =
        let harness = seeded ()
        harness.Run "save-view weekend $row.kind eq folder" |> ignore

        Assert.AreEqual<string list>([ "|"; "documents/"; "examples/"; "guide/"; "projects/"; "weekend" ], texts harness "ls ")
        Assert.AreEqual<string list>([ "documents/"; "examples/"; "guide/"; "projects/"; "weekend" ], texts harness "cp readme.txt ")

    [<TestMethod>]
    member _.APathOffersFilesAndFolders() =
        let harness = seeded ()
        let offered = texts harness "read "

        assertContains "readme.txt" offered
        assertContains "documents/" offered

    /// A word past what the command takes is offered files, as every word was before.
    [<TestMethod>]
    member _.ASurplusWordOffersFiles() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "readme.txt" ], texts harness "read x re")
        Assert.AreEqual<string list>([ "readme.txt" ], texts harness "frob re")

    // ---------------------------------------- decision 0039: the pipe first

    /// `vars` takes nothing, so after it there is nothing to offer but the pipe: not
    /// the files it used to offer (R7).
    [<TestMethod>]
    member _.ACommandThatTakesNothingOffersOnlyThePipe() =
        let harness = seeded ()

        match items harness "vars " with
        | [ pipe ] ->
            Assert.AreEqual<string>("|", pipe.Text)
            Assert.AreEqual<string>("operator", pipe.Kind)
            Assert.AreEqual<string option>(Some "send the result on", pipe.Detail)
            Assert.AreEqual<int>(5, pipe.Start)
            Assert.AreEqual<int>(5, pipe.End)
        | other -> Assert.Fail(sprintf "%A" other)

    /// Every argument written, or every parameter fed by the pipe: only the pipe.
    [<TestMethod>]
    member _.AStageWithNothingLeftToTakeOffersOnlyThePipe() =
        let harness = seeded ()

        for line in [ "vars "; "pwd "; "ls | count "; "ls | first "; "cp readme.txt documents "; "echo hello "; "read readme.txt " ] do
            Assert.AreEqual<string list>([ "|" ], texts harness line, line)

    /// `ls `'s path is optional: the pipe comes first, then what it offered before.
    [<TestMethod>]
    member _.ACompleteStageOffersThePipeFirst() =
        let harness = seeded ()

        match texts harness "ls " with
        | "|" :: rest ->
            Assert.AreEqual<string list>([ "documents/"; "examples/"; "guide/"; "projects/" ], rest)
        | other -> Assert.Fail(sprintf "%A" other)

        for line in [ "help "; "ls | sort name "; "ls | select name "; "ls | distinct "; "attr readme.txt " ] do
            Assert.AreEqual<string>("|", List.head (texts harness line), line)

    /// A required argument not yet written is what is offered, and the pipe is not.
    [<TestMethod>]
    member _.AStageMissingARequiredArgumentOffersNoPipe() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "name"; "kind"; "folder"; "size"; "modified" ], texts harness "ls | sort ")

        // `read`'s path takes the pipe, and nothing feeds it at the head of a line;
        // `select` with no column is a fault although its columns are a `Rest`.
        for line in [ "ls | sort "; "read "; "cp "; "cp readme.txt "; "ls | select "; "ls | take "; "set "; "set x "; "echo "; "mkdir " ] do
            assertDoesNotContain "|" (texts harness line)

    /// Only where nothing of the word is written, and not in what is not a command.
    [<TestMethod>]
    member _.ThePipeIsOfferedOnlyForAnEmptyWordOfACommand() =
        let harness = seeded ()

        for line in [ "ls d"; "ls \""; "vars x"; "frob "; "ls -" ] do
            assertDoesNotContain "|" (texts harness line)

    /// After `-desc` the word may still be the switch's value, so that follows the pipe.
    [<TestMethod>]
    member _.AFlagsValueFollowsThePipe() =
        let harness = seeded ()

        Assert.AreEqual<string list>([ "|"; "desc"; "asc" ], texts harness "ls | sort name -desc ")

    // ------------------------------------------------------- else

    /// `else` is offered where an argument is written once two letters of it are
    /// there, whatever the parameter takes, as the lexical rules offered it.
    [<TestMethod>]
    member _.ElseIsOfferedInEveryArgumentPlace() =
        let harness = seeded ()

        for line in [ "read x el"; "read el"; "ls | sort name el"; "ls | take el"; "set el"; "attr readme.txt el" ] do
            assertContains "else" (texts harness line)

        for line in [ "read x e"; "ls | sort name -el" ] do
            assertDoesNotContain "else" (texts harness line)

    // ------------------------------------------------------- a command name

    /// No command takes a command name until stream A gives `help` one, so this asks a
    /// command made for the test.
    [<TestMethod>]
    member _.ACommandNameOffersTheCommands() =
        let harness = seeded ()

        let explain =
            CommandSpec.create
                "explain"
                "Say what a command does"
                []
                [ Parameter.create "command" "The command" |> Parameter.takes Takes.CommandName ]

        let specs = explain :: harness.Session.Commands
        let text = "explain wh"

        let request: Request =
            { Specs = specs
              Projection = harness.Projection
              Context = Context.analyse specs text text.Length
              Shapes = fun _ -> async.Return { Columns = []; Rows = None }
              Cancel = CancellationToken.None }

        let answer = Async.RunSynchronously(Completion.complete request)

        match answer.Items with
        | [ where ] ->
            Assert.AreEqual<string>("where", where.Text)
            Assert.AreEqual<string>("command", where.Kind)
            Assert.AreEqual<string option>(Some "Keep the rows a predicate is true for", where.Detail)
        | other -> Assert.Fail(sprintf "%A" other)

        Assert.AreEqual<int option>(Some 0, answer.Signature.Value.Active)
