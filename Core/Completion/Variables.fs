/// Variables, their members, and tags (stream C).
namespace CommandLineReimagined.Core

open System

[<RequireQualifiedAccess>]
module VariableCompletion =

    let private startsWith (prefix: string) (candidate: string) =
        candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)

    let private ordinal (a: string) (b: string) = String.CompareOrdinal(a, b)

    /// The variable a predicate binds to the row it is testing (decision 0008).
    let rowName = "row"

    /// What `$row`'s chip says, since it holds nothing until a predicate runs.
    let rowDetail = "the row being tested"

    // ---------------------------------------------------------------- Variables

    /// <summary>After `$`: the variables in scope, each with what it holds.</summary>
    /// <remarks>
    /// `$row` is offered only where `inPredicate`, and first, because it is the only
    /// place it exists and there it is nearly always the one wanted (finding 2). A
    /// variable bound as `row` is shadowed there, as it is when the predicate runs.
    /// After `<$` the variables complete with that sigil.
    /// </remarks>
    let variables (request: Request) (inPredicate: bool) : Async<Completion list> =
        let prefix = request.Context.Word.Prefix
        let sigil = if prefix.StartsWith "<$" then "<$" else "$"
        let written = if prefix.StartsWith sigil then prefix.Substring sigil.Length else prefix

        let row =
            if inPredicate && startsWith written rowName then
                [ Request.item request "variable" (sigil + rowName) |> Request.withDetail rowDetail ]
            else
                []

        let bound =
            request.Projection.Variables
            |> Map.toList
            |> List.filter (fun (name, _) -> startsWith written name && not (inPredicate && name = rowName))
            |> List.sortWith (fun (a, _) (b, _) -> ordinal a b)
            |> List.map (fun (name, value) ->
                Request.item request "variable" (sigil + name) |> Request.withDetail (Summary.ofValue value))

        async.Return(row @ bound)

    // ---------------------------------------------------------------- Members

    /// <summary>The members a value has, each with a line about it.</summary>
    /// <remarks>
    /// A tag's are its attributes, a file's what `Expr.readMember` reads off one, and a
    /// fault's what decision 0014 lets a script ask of it, each with the summary of what
    /// it holds. A number, a text or a boolean has none, and offering a listing's
    /// columns for one was finding 5. Nor does a table: `Expr.readMember` reads nothing
    /// off one, since a column is read off a row, so offering `$files.name` offered a
    /// member that answers nothing. The owner chose on 2026-09-23 to stop offering
    /// them rather than make a table's member mean something. `$row.` is unaffected:
    /// a row is a tag, and its columns come from what flows into the stage.
    /// </remarks>
    let private membersOf (value: Value) : (string * string) list =
        let read names =
            names |> List.map (fun name -> name, Summary.ofValue (Expr.readMember name value))

        match value with
        | Value.Object tag
        | Value.Component tag -> Value.orderedAttributes tag |> List.map (fun (name, v) -> name, Summary.ofValue v)
        | Value.File _ -> read [ "name"; "kind"; "folder"; "path"; "id" ]
        | Value.Fault fault -> read ([ "kind"; "message"; "stage"; "path" ] @ (if fault.Cause.IsSome then [ "cause" ] else []))
        | _ -> []

    /// <summary>`$row.a.`: the members of what column `a` holds, in the first row that has it.</summary>
    /// <remarks>Only when the upstream was run, so there are rows to look in.</remarks>
    let private rowMembers (shape: Shape) (path: string list) =
        match path, shape.Rows with
        | [], _ -> shape.Columns |> List.map (fun (name, columnType) -> name, Summary.columnType columnType)
        | column :: rest, Some rows ->
            rows
            |> List.tryPick (fun row ->
                match Map.tryFind column row with
                | Some value ->
                    match List.fold (fun current name -> Expr.readMember name current) value rest with
                    | Value.None
                    | Value.Empty -> None
                    | found -> Some(membersOf found)
                | None -> None)
            |> Option.defaultValue []
        | _ :: _, None -> []

    /// <summary>After `$name.`: the members of what the variable holds.</summary>
    /// <remarks>
    /// `path` is followed the way the evaluator reads members, so `$x.a.` offers the
    /// members of `$x.a`. `$row.` asks what flows into the stage it is in (`Shape`),
    /// or what a listing of the current folder would have when it is in none. A
    /// completion replaces the whole word, so its text is `$row.kind`, not `kind`.
    /// </remarks>
    let members (request: Request) (variable: string) (path: string list) (stage: Stage option) : Async<Completion list> =
        async {
            let prefix = request.Context.Word.Prefix
            let stop = prefix.LastIndexOf '.'
            let stem = prefix.Substring(0, stop + 1)
            let written = prefix.Substring(stop + 1)

            let! candidates =
                if variable = rowName then
                    async {
                        let! shape =
                            match stage with
                            | Some stage -> request.Shapes stage
                            | None -> async.Return(Shape.ofListing request.Projection)

                        return rowMembers shape path
                    }
                else
                    match Map.tryFind variable request.Projection.Variables with
                    | Some value ->
                        async.Return(membersOf (List.fold (fun current name -> Expr.readMember name current) value path))
                    | None -> async.Return []

            return
                candidates
                |> List.distinctBy fst
                |> List.filter (fun (name, _) -> startsWith written name)
                |> List.map (fun (name, detail) -> Request.item request "member" (stem + name) |> Request.withDetail detail)
        }

    // ---------------------------------------------------------------- Tags

    /// The kinds a tag never makes: `mkdir` makes a folder and `save-view` a view.
    let private notTagTypes = [ Value.folderKind; Value.viewKind ]

    /// Every tag a variable holds, on its own or in a list.
    let private variableTags (projection: Projection) : Tag list =
        projection.Variables
        |> Map.toList
        |> List.collect (fun (_, value) ->
            match value with
            | Value.Object tag -> [ tag ]
            | Value.List items -> items |> List.choose (function Value.Object tag -> Some tag | _ -> None)
            | _ -> [])

    let private records (projection: Projection) = projection.Files |> Map.toList |> List.map snd

    let private plural (n: int) (word: string) = sprintf "%d %s%s" n word (if n = 1 then "" else "s")

    /// <summary>After `<`: the tag types in use.</summary>
    /// <remarks>
    /// A saved tag's type is its record's kind (decision 0027), so the types in use are
    /// the kinds of the records in the filesystem, and the types of the tags variables
    /// hold. Folders and views are left out: they are made by `mkdir` and `save-view`.
    /// </remarks>
    let tagTypes (request: Request) : Async<Completion list> =
        let prefix = request.Context.Word.Prefix
        let written = if prefix.StartsWith "<" then prefix.Substring 1 else prefix

        let kinds =
            records request.Projection
            |> List.map Record.kind
            |> List.filter (fun kind -> kind <> "" && not (List.contains kind notTagTypes))
            |> List.countBy id
            |> Map.ofList

        let held =
            variableTags request.Projection
            |> List.map (fun tag -> tag.TypeName)
            |> List.filter (fun name -> name <> "")
            |> List.countBy id
            |> Map.ofList

        let detail (typeName: string) =
            [ Map.tryFind typeName kinds |> Option.map (fun n -> plural n "record")
              Map.tryFind typeName held |> Option.map (fun n -> plural n "tag" + " in variables") ]
            |> List.choose id
            |> String.concat " · "

        (Map.keys kinds |> List.ofSeq) @ (Map.keys held |> List.ofSeq)
        |> List.distinct
        |> List.filter (startsWith written)
        |> List.sortWith ordinal
        |> List.map (fun typeName -> Request.item request "keyword" ("<" + typeName) |> Request.withDetail (detail typeName))
        |> async.Return

    /// <summary>The attribute names already written in the open tag the word is in.</summary>
    /// <remarks>
    /// Read from the text between the tag's `<type` and the word, skipping quoted
    /// values, so `<note title="a b=c" ` has written `title` and nothing else.
    /// </remarks>
    let private writtenAttributes (text: string) (typeName: string) (upTo: int) : Set<string> =
        let before = text.Substring(0, upTo)
        let opening = before.LastIndexOf("<" + typeName)

        if opening < 0 then
            Set.empty
        else
            let segment = before.Substring(opening + 1 + typeName.Length)
            let words = System.Collections.Generic.List<string>()
            let current = System.Text.StringBuilder()
            let mutable quoted = false

            for c in segment do
                if c = '"' then
                    quoted <- not quoted
                    current.Append c |> ignore
                elif Char.IsWhiteSpace c && not quoted then
                    if current.Length > 0 then words.Add(current.ToString())
                    current.Clear() |> ignore
                else
                    current.Append c |> ignore

            if current.Length > 0 then words.Add(current.ToString())

            words
            |> Seq.choose (fun word ->
                match word.IndexOf '=' with
                | index when index > 0 -> Some(word.Substring(0, index))
                | _ -> None)
            |> Set.ofSeq

    /// <summary>Inside `<type `: the attribute names records of that type carry, as `name=`.</summary>
    /// <remarks>
    /// From the records of that kind and the tags of that type variables hold. `name`
    /// comes first, since `save` needs it; the runtime's own attributes (`kind`,
    /// `folder`, `created`, `modified`, `size`) are not written in a tag. A name already
    /// in the tag is not offered again.
    /// </remarks>
    let tagAttributes (request: Request) (typeName: string) : Async<Completion list> =
        let context = request.Context

        let fromRecords =
            records request.Projection
            |> List.filter (fun record -> Record.kind record = typeName)
            |> List.map (fun record -> record.Attributes |> Map.toList |> List.map fst)

        let fromTags =
            variableTags request.Projection
            |> List.filter (fun tag -> tag.TypeName = typeName)
            |> List.map (fun tag -> tag.Attributes |> Map.toList |> List.map fst)

        let carriers = fromRecords @ fromTags
        let total = List.length carriers
        let written = writtenAttributes context.Text typeName context.Word.Start

        let counts =
            carriers
            |> List.collect List.distinct
            |> List.filter (fun name -> name = Attributes.name || not (List.contains name Attributes.owned))
            |> List.countBy id

        let named, others = counts |> List.partition (fun (name, _) -> name = Attributes.name)

        named @ (others |> List.sortWith (fun (a, _) (b, _) -> ordinal a b))
        |> List.filter (fun (name, _) -> not (written.Contains name) && startsWith context.Word.Prefix name)
        |> List.map (fun (name, carried) ->
            Request.item request "member" (name + "=")
            |> Request.withDetail (sprintf "%d of %d carry it" carried total))
        |> async.Return
