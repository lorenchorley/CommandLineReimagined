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
    /// completion, rather than the page and the core each having half of it.
    let private pageWords = [ "help"; "clear" ]

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
        elif word.StartsWith "$" then
            let prefix = word.Substring 1

            projection.Variables
            |> Map.toList
            |> List.filter (fun (name, _) -> name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            |> List.map (fun (name, _) -> { Kind = "variable"; Text = "$" + name; Start = start })
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

            if not (List.isEmpty commands) then
                commands
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

                if not (Files.folderExists projection folder) then
                    []
                else
                    Files.inFolder projection folder
                    |> List.filter (fun record ->
                        (Record.name record).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    |> List.sortWith (fun a b -> String.CompareOrdinal(Record.name a, Record.name b))
                    |> List.map (fun record ->
                        if Record.isFolder record then
                            { Kind = "folder"
                              Text = written + Record.name record + "/"
                              Start = start }
                        else
                            { Kind = "file"
                              Text = written + Record.name record
                              Start = start })
