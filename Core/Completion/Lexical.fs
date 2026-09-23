/// The lexical rules: what completion answered before it read the line (Phase 8).
///
/// Kept, not deleted (decision 0031). They answer when the line cannot be read, and
/// until a stream gives a place its own provider they answer that place too, so the
/// page completes exactly as it did. They guess from the text: the last word, whether
/// there is a `$` earlier in the stage, and otherwise the files in the current folder.
namespace CommandLineReimagined.Core

open System

[<RequireQualifiedAccess>]
module Lexical =

    /// Words the page handles itself. They are listed here so one list drives
    /// completion, rather than the page and the core each having half of it. `help`
    /// left this list in Phase 3: it is a command now.
    let pageWords = [ "clear" ]

    /// The word operators, offered after an operand. Phone keyboards have no Tab key
    /// and these are the words a predicate is made of, so a tap is the whole of how
    /// most people will write one.
    let operatorWords =
        [ "and"; "eq"; "ge"; "gt"; "has"; "le"; "like"; "lt"; "ne"; "not"; "or" ]

    /// <summary>The column names a predicate could name after `$row.`.</summary>
    /// <remarks>
    /// There is no table in scope while a line is being typed, so these are the columns
    /// a listing of where you are would have: the five a record always has, and every
    /// attribute anything here carries. It is a guess, and it is the right one for
    /// `ls | where $row.` — which is what a predicate is nearly always written against.
    /// </remarks>
    let columnNames (projection: Projection) =
        let attributes =
            Files.inFolder projection projection.Location.Folder
            |> List.collect (fun record -> record.Attributes |> Map.toList |> List.map fst)
            |> List.filter (fun name -> not (List.contains name Attributes.owned))
            |> List.distinct
            |> List.sortWith (fun a b -> String.CompareOrdinal(a, b))

        Table.recordColumns @ attributes

    /// <summary>The words that shape a line rather than being part of a stage (Phase 5).</summary>
    /// <remarks>
    /// `try` is offered where a command would be, since that is where it is written.
    /// `else` is offered where an argument would be, and only once two letters of it are
    /// there, because `e` alone is far more often the start of a file name.
    /// </remarks>
    let stageKeywords = [ "try" ]

    let lineKeywords = [ "else" ]

    let private isWordBoundary (c: char) =
        Char.IsWhiteSpace c || c = '|' || c = '(' || c = ','

    let private lastWordStart (text: string) =
        let mutable index = text.Length

        while index > 0 && not (isWordBoundary text[index - 1]) do
            index <- index - 1

        index

    /// Today's answer for the text, as if the cursor were at its end.
    let suggest (specs: CommandSpec list) (projection: Projection) (text: string) : Completion list =
        let text = if isNull text then "" else text
        let start = lastWordStart text
        let word = text.Substring start
        let before = text.Substring(0, start)

        // At the head of a line, straight after a pipe, and — from Phase 5 — after
        // `else`, after `try` and inside an opening parenthesis, the word names a
        // command.
        let firstWord =
            let trimmed = before.TrimEnd()
            let lastToken = trimmed.Split([| ' '; '\t'; '|'; '(' |]) |> Array.last

            trimmed.Length = 0
            || trimmed.EndsWith '|'
            || trimmed.EndsWith '('
            || (trimmed.Length < before.Length && (lastToken = "else" || lastToken = "try"))

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
            |> List.map (fun name -> { Kind = "member"; Text = stem + name; Start = start; End = text.Length; Detail = None })
        elif word.StartsWith "$" then
            let prefix = word.Substring 1

            // `row` is not in the projection: it exists only while a predicate is being
            // evaluated. It is offered anyway, because it is the one variable a user
            // writes without ever having bound it.
            ((projection.Variables |> Map.toList |> List.map fst) @ [ "row" ])
            |> List.distinct
            |> List.filter (fun name -> name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            |> List.sortWith (fun a b -> String.CompareOrdinal(a, b))
            |> List.map (fun name -> { Kind = "variable"; Text = "$" + name; Start = start; End = text.Length; Detail = None })
        else
            let commands =
                if firstWord then
                    (specs |> List.map (fun spec -> spec.Name) |> List.filter startsWith
                     |> List.map (fun name -> { Kind = "command"; Text = name; Start = start; End = text.Length; Detail = None }))
                    @ (pageWords
                       |> List.filter startsWith
                       |> List.map (fun name -> { Kind = "command"; Text = name; Start = start; End = text.Length; Detail = None }))
                    @ (stageKeywords
                       |> List.filter startsWith
                       |> List.map (fun name -> { Kind = "keyword"; Text = name; Start = start; End = text.Length; Detail = None }))
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
                    |> List.map (fun name -> { Kind = "operator"; Text = name; Start = start; End = text.Length; Detail = None })

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

                let keywords =
                    if not firstWord && word.Length >= 2 && written = "" then
                        lineKeywords
                        |> List.filter startsWith
                        |> List.map (fun name -> { Kind = "keyword"; Text = name; Start = start; End = text.Length; Detail = None })
                    else
                        []

                if not (Files.folderExists projection folder) then
                    keywords
                else
                    keywords
                    @ (Files.inFolder projection folder
                    |> List.filter (fun record ->
                        (not places || Record.isFolder record || Record.kind record = Value.viewKind)
                        && (Record.name record).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    |> List.sortWith (fun a b -> String.CompareOrdinal(Record.name a, Record.name b))
                    |> List.map (fun record ->
                        if Record.isFolder record then
                            { Kind = "folder"
                              Text = written + Record.name record + "/"
                              Start = start; End = text.Length; Detail = None }
                        else
                            // A view is a place without being a path, so it completes as
                            // its own name: `cd weekend/` would name nothing.
                            { Kind = (if Record.kind record = Value.viewKind then Value.viewKind else "file")
                              Text = written + Record.name record
                              Start = start; End = text.Length; Detail = None }))

    /// <summary>The lexical answer for a request, at its cursor.</summary>
    /// <remarks>
    /// The rules read the text before the cursor, so a completion replaces up to the
    /// end of the word, which may be past the cursor.
    /// </remarks>
    let answer (request: Request) : Completion list =
        let context = request.Context

        suggest request.Specs request.Projection (context.Text.Substring(0, context.Cursor))
        |> List.map (fun completion -> { completion with End = max completion.Start context.Word.End })
