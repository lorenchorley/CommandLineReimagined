/// Files, folders and views, by the path written so far (stream D).
namespace CommandLineReimagined.Core

open System

[<RequireQualifiedAccess>]
module PathCompletion =

    /// The grammar's identifier characters (`Grammar.isIdentifierChar`).
    let private accented =
        "éèàäëïöüùçâêîôûÇÄÅÉæÆÖÜøØƒáíóúñÑÁÂÀãÃðÐÊËÈiÍÎÏÌÓßÔÒõÕµþÞÚÛÙýÝ"

    let private isIdentifierChar (c: char) =
        (c >= 'a' && c <= 'z')
        || (c >= 'A' && c <= 'Z')
        || (c >= '0' && c <= '9')
        || c = '_'
        || accented.IndexOf c >= 0

    /// <summary>Whether a bare word can carry a name as it is.</summary>
    /// <remarks>
    /// The grammar's bare word (`Grammar.isWordStart`, `isWordChar`): identifier
    /// characters and `.\/:~+@%-*`, starting with an identifier character or `.\/~*`.
    /// A name with anything else in it, a space most often, is only a name when it is
    /// quoted, so it completes quoted.
    /// </remarks>
    let private bare (text: string) =
        text.Length > 0
        && (isIdentifierChar text[0] || ".\\/~*".IndexOf text[0] >= 0)
        && text |> Seq.forall (fun c -> isIdentifierChar c || ".\\/:~+@%-*".IndexOf c >= 0)

    /// <summary>The records a path could name.</summary>
    /// <remarks>
    /// The part of the word before its last separator says which folder to look in, and
    /// the rest is the prefix to match. `places` keeps to folders and saved views, the
    /// things you can be in (decision 0013), for a parameter that takes a place.
    ///
    /// A quoted word completes quoted, `read "doc` to `"documents/"`, and so does a name
    /// a bare word cannot carry. Each completion replaces the whole word, quotes
    /// included, from `Word.Start` to `Word.End`.
    /// </remarks>
    let suggest (request: Request) (places: bool) : Completion list =
        let projection = request.Projection
        let word = request.Context.Word
        let prefixed = word.Prefix

        let separator = prefixed.LastIndexOf '/'
        let written = if separator >= 0 then prefixed.Substring(0, separator + 1) else ""
        let prefix = if separator >= 0 then prefixed.Substring(separator + 1) else prefixed

        let folder =
            if written = "" then
                projection.Location.Folder
            else
                Files.normalise projection.Location.Folder written

        let quoted (text: string) =
            if word.Quoted || not (bare text) then "\"" + text + "\"" else text

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
                    Request.item request "folder" (quoted (written + Record.name record + "/"))
                else
                    // A view is a place without being a path, so it completes as its
                    // own name: `in weekend/` would name nothing.
                    let kind = if Record.kind record = Value.viewKind then Value.viewKind else "file"
                    Request.item request kind (quoted (written + Record.name record)))
