/// What the text could continue as.
///
/// Phone keyboards have no Tab key, so this is the only completion most users will
/// have: the page shows the answers as chips and substitutes one on a tap. Whole
/// replacements for the last word rather than suffixes, so the client does not have to
/// know where a word began.
namespace CommandLineReimagined.Core

open System

type Completion = { Kind: string; Text: string; Start: int }

[<RequireQualifiedAccess>]
module Completion =

    /// Words the page handles itself. They are listed here so one list drives
    /// completion, rather than the page and the core each having half of it. `help`
    /// left this list in Phase 3: it is a command now.
    let private pageWords = [ "clear" ]

    /// The word operators, offered after an operand. Phone keyboards have no Tab key
    /// and these are the words a predicate is made of, so a tap is the whole of how
    /// most people will write one.
    let private operatorWords =
        [ "and"; "eq"; "ge"; "gt"; "has"; "le"; "like"; "lt"; "ne"; "not"; "or" ]

    /// <summary>The column names a predicate could name after `$row.`.</summary>
    /// <remarks>
    /// There is no table in scope while a line is being typed, so these are the columns
    /// a listing of where you are would have: the five a record always has, and every
    /// attribute anything here carries. It is a guess, and it is the right one for
    /// `ls | where $row.` — which is what a predicate is nearly always written against.
    /// </remarks>
    let private columnNames (projection: Projection) =
        let attributes =
            Files.inFolder projection projection.Location.Folder
            |> List.collect (fun record -> record.Attributes |> Map.toList |> List.map fst)
            |> List.filter (fun name -> not (List.contains name Attributes.reserved))
            |> List.distinct
            |> List.sortWith (fun a b -> String.CompareOrdinal(a, b))

        Table.recordColumns @ attributes

    let private isWordBoundary (c: char) =
        Char.IsWhiteSpace c || c = '|' || c = '(' || c = ','

    let private lastWordStart (text: string) =
        let mutable index = text.Length

        while index > 0 && not (isWordBoundary text[index - 1]) do
            index <- index - 1

        index

    let suggest (specs: CommandSpec list) (projection: Projection) (text: string) : Completion list =
        let text = if isNull text then "" else text
        let start = lastWordStart text
        let word = text.Substring start
        let before = text.Substring(0, start)

        // At the head of a line, or straight after a pipe, the word names a command.
        let firstWord = before.Trim().Length = 0 || before.TrimEnd().EndsWith '|'

        let startsWith (candidate: string) =
            candidate.StartsWith(word, StringComparison.OrdinalIgnoreCase)

        // Nothing typed yet: offering every command is noise, and the page shows its
        // suggestion chips in that state instead.
        if firstWord && word.Length = 0 then
            []
        elif word.StartsWith "$" && word.Contains "." then
            // `$row.` reads a column, so what follows the stop is a column name rather
            // than a variable (decision 0008).
            let stop = word.LastIndexOf '.'
            let stem = word.Substring(0, stop + 1)
            let prefix = word.Substring(stop + 1)

            columnNames projection
            |> List.filter (fun name -> name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            |> List.map (fun name -> { Kind = "member"; Text = stem + name; Start = start })
        elif word.StartsWith "$" then
            let prefix = word.Substring 1

            // `row` is not in the projection: it exists only while a predicate is being
            // evaluated. It is offered anyway, because it is the one variable a user
            // writes without ever having bound it.
            ((projection.Variables |> Map.toList |> List.map fst) @ [ "row" ])
            |> List.distinct
            |> List.filter (fun name -> name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            |> List.sortWith (fun a b -> String.CompareOrdinal(a, b))
            |> List.map (fun name -> { Kind = "variable"; Text = "$" + name; Start = start })
        else
            let commands =
                if firstWord then
                    (specs |> List.map (fun spec -> spec.Name) |> List.filter startsWith
                     |> List.map (fun name -> { Kind = "command"; Text = name; Start = start }))
                    @ (pageWords
                       |> List.filter startsWith
                       |> List.map (fun name -> { Kind = "command"; Text = name; Start = start }))
                else
                    []

            // <summary>Operators, but only where an expression is plainly being written.</summary>
            //
            // The test is a `$` earlier in this stage. A predicate names its row
            // (decision 0008), so one is always there, and without the test `cat no`
            // would offer `not` beside `notes.txt` — which is the commoner line by far
            // and the one that must not be made worse. Never straight after another
            // operator, because two in a row is not a thing.
            let stage =
                match before.LastIndexOf '|' with
                | -1 -> before
                | index -> before.Substring(index + 1)

            let lastWritten =
                stage.TrimEnd().Split(' ', '\t') |> Array.tryLast |> Option.defaultValue ""

            let operators =
                if firstWord || not (stage.Contains "$") || List.contains lastWritten operatorWords then
                    []
                else
                    operatorWords
                    |> List.filter startsWith
                    |> List.map (fun name -> { Kind = "operator"; Text = name; Start = start })

            if not (List.isEmpty commands) then
                commands
            elif not (List.isEmpty operators) then
                operators
            else
                // A path: the part before the last separator says which folder to look
                // in, and the rest is the prefix to match.
                let separator = word.LastIndexOf '/'
                let written = if separator >= 0 then word.Substring(0, separator + 1) else ""
                let prefix = if separator >= 0 then word.Substring(separator + 1) else word

                let folder =
                    if written = "" then
                        projection.Location.Folder
                    else
                        Files.normalise projection.Location.Folder written

                // After `cd `, only the things you can be in: folders, and the saved
                // views that are places too (decision 0013). Offering every file after
                // a word that takes a place is offering answers that cannot be right.
                let places =
                    (stage.TrimStart().Split(' ', '\t') |> Array.tryHead |> Option.defaultValue "")
                        .Equals("cd", StringComparison.OrdinalIgnoreCase)

                if not (Files.folderExists projection folder) then
                    []
                else
                    Files.inFolder projection folder
                    |> List.filter (fun record ->
                        (not places || Record.isFolder record || Record.kind record = Value.viewKind)
                        && (Record.name record).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    |> List.sortWith (fun a b -> String.CompareOrdinal(Record.name a, Record.name b))
                    |> List.map (fun record ->
                        if Record.isFolder record then
                            { Kind = "folder"
                              Text = written + Record.name record + "/"
                              Start = start }
                        else
                            // A view is a place without being a path, so it completes as
                            // its own name: `cd weekend/` would name nothing.
                            { Kind = (if Record.kind record = Value.viewKind then Value.viewKind else "file")
                              Text = written + Record.name record
                              Start = start })
