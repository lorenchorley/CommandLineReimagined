/// What a value is, in one line (Phase 8).
///
/// For a completion chip's detail, for hover and for `vars`, so that all three describe
/// a value in the same words.
namespace CommandLineReimagined.Core

[<RequireQualifiedAccess>]
module Summary =

    /// How much of a free-running part (a text, a message, a query) is kept.
    let private limit = 40

    /// One line, cut short with an ellipsis.
    let private cut (text: string) =
        let text = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ')

        if text.Length <= limit then text else text.Substring(0, limit - 1) + "…"

    let private count (n: int) (singular: string) (plural: string) =
        sprintf "%d %s" n (if n = 1 then singular else plural)

    /// How many column names a table's summary names before it stops.
    let private columnsNamed = 3

    let private join (parts: string list) = parts |> List.filter (fun p -> p <> "") |> String.concat " · "

    /// <summary>A column's type as a word.</summary>
    /// <remarks>The words `columns` answers, so a member's detail reads like its row there.</remarks>
    let columnType (columnType: ColumnType) =
        match columnType with
        | TextCol -> "text"
        | NumberCol -> "number"
        | BooleanCol -> "boolean"
        | FileCol -> "file"
        | ObjectCol -> "object"
        | MixedCol -> "mixed"

    let private columns (table: Table) =
        let names = table.Columns |> List.map (fun column -> column.Name)

        if List.length names <= columnsNamed then
            String.concat ", " names
        else
            (names |> List.take columnsNamed |> String.concat ", ") + "…"

    let private tag (word: string) (tag: Tag) =
        join
            [ word
              tag.TypeName
              count (Map.count tag.Attributes) "attribute" "attributes"
              (if List.isEmpty tag.Children then "" else count (List.length tag.Children) "child" "children") ]

    /// <summary>A value in one line, with a file's size when `sizeOf` knows it.</summary>
    /// <remarks>
    /// A file value carries its name, kind and folder, not its size: `size` is the
    /// content's length and is not stored (decision 0013), so it takes a read of the
    /// content, which only something holding the blobs can do. `ofValue` is this with
    /// no sizes, and is what completion and `vars` use, so that they agree.
    /// </remarks>
    let ofValueWith (sizeOf: FileRef -> float option) (value: Value) : string =
        match value with
        | Value.Empty -> "empty"
        | Value.None -> "none"
        | Value.Number n -> "number · " + Value.formatNumber n
        | Value.Boolean b -> "boolean · " + (if b then "true" else "false")
        | Value.Text text -> "text · \"" + cut text + "\""
        | Value.File file when file.Kind = Value.folderKind -> "folder · " + file.Name
        | Value.File file ->
            join
                [ "file"
                  file.Name
                  file.Kind
                  (match sizeOf file with
                   | Some size -> Value.formatNumber size + " bytes"
                   | None -> "") ]
        | Value.List items -> "list · " + count (List.length items) "item" "items"
        | Value.Object t -> tag "tag" t
        | Value.Component t -> tag "component" t
        | Value.Table table -> join [ "table"; count (List.length table.Rows) "row" "rows"; columns table ]
        | Value.Query expr -> "query · " + cut (Value.exprText expr)
        | Value.Fault fault -> join [ "fault"; FaultKind.name fault.Kind; cut fault.Message ]

    /// <summary>What a value is, in one line: `number · 5`, `table · 4 rows · name, kind, folder…`.</summary>
    /// <remarks>
    /// The kind first, as `$problem.kind` and the page would name it, then what the value
    /// itself carries. Nothing is looked up, so a summary never needs the store.
    /// </remarks>
    let ofValue (value: Value) : string = ofValueWith (fun _ -> None) value
