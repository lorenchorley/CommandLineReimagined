/// Rows and columns.
///
/// Decision 0009: the tag notation is already table-shaped — a parent whose children
/// share a type, each carrying attributes — so a table is not a new notation, it is a
/// reading of one that exists. A tag that is not table-shaped stays a tree and says
/// which child broke the shape.
namespace CommandLineReimagined.Core

open System

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Table =

    /// The type name a row gets when nothing else names it.
    let rowType = "row"

    let empty = { Columns = []; Rows = [] }

    let column name kind = { Name = name; Type = kind }

    let names (table: Table) = table.Columns |> List.map (fun c -> c.Name)

    let indexOf (table: Table) (name: string) =
        table.Columns
        |> List.tryFindIndex (fun c -> String.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))

    /// A cell by column name. A column the table does not have reads as `None`, which is
    /// the same answer a row missing that column gives.
    let cell (table: Table) (name: string) (row: Value list) =
        match indexOf table name with
        | Some index when index < List.length row -> List.item index row
        | _ -> Value.None

    // ------------------------------------------------------------- Column types

    let private typeOf (value: Value) =
        match value with
        | Value.Number _ -> Some NumberCol
        | Value.Boolean _ -> Some BooleanCol
        | Value.File _ -> Some FileCol
        | Value.Object _
        | Value.Component _ -> Some ObjectCol
        | Value.Empty
        | Value.None -> Option.None
        | _ -> Some TextCol

    /// <summary>The type of a column, from the cells in it.</summary>
    /// <remarks>
    /// Decision 0009: typed when every cell that is not a gap agrees, `MixedCol` when
    /// they do not, and text when there is nothing to go on. Gaps are ignored rather
    /// than making the column mixed, or a single missing cell would turn a number
    /// column into one that sorts alphabetically.
    /// </remarks>
    let inferType (cells: Value list) =
        match cells |> List.choose typeOf |> List.distinct with
        | [] -> TextCol
        | [ single ] -> single
        | _ -> MixedCol

    /// Builds a table from named columns whose types are read off the rows.
    let ofColumns (names: string list) (rows: Value list list) =
        let columns =
            names
            |> List.mapi (fun index name ->
                column name (inferType (rows |> List.map (fun row -> List.item index row))))

        { Columns = columns; Rows = rows }

    // ------------------------------------------------------------------ Records

    /// The attributes a listing shows in a column of its own, in that order. `created`
    /// is deliberately absent: every record has one, it is never what a listing is for,
    /// and a column nobody reads costs a phone screen more than it is worth. `attr`
    /// shows it.
    let recordColumns = [ Attributes.name; Attributes.kind; Attributes.folder; "size"; Attributes.modified ]

    /// <summary>The table a listing is.</summary>
    /// <remarks>
    /// `size` is the content's length, which is not stored (decision 0013), so it is
    /// passed in: only the caller has the blob store, and computing it here would mean
    /// handing the whole log to a pure function to look up one number.
    /// </remarks>
    let ofRecords (size: FileRecord -> float) (records: FileRecord list) : Table =
        let extra =
            records
            |> List.collect (fun record -> record.Attributes |> Map.toList |> List.map fst)
            |> List.filter (fun name -> not (List.contains name Attributes.reserved))
            |> List.distinct
            |> List.sortWith (fun a b -> String.CompareOrdinal(a, b))

        let attribute (record: FileRecord) name =
            match Map.tryFind name record.Attributes with
            | Some value -> value
            | Option.None -> Value.None

        let rows =
            records
            |> List.map (fun record ->
                [ Record.toValue record
                  Value.Text(Record.kind record)
                  Value.Text(Record.folder record)
                  Value.Number(size record)
                  attribute record Attributes.modified ]
                @ (extra |> List.map (attribute record)))

        ofColumns (recordColumns @ extra) rows

    // --------------------------------------------------------------------- Tags

    let private childTag (child: Value) =
        match child with
        | Value.Object tag
        | Value.Component tag -> Some tag
        | _ -> Option.None

    /// <summary>Reads a table out of a list of same-typed tags.</summary>
    /// <remarks>
    /// The columns are the union of the children's attributes in the order they first
    /// appear, so a table written out of an XML file keeps the order the document had
    /// rather than an alphabetical one, and a missing cell is `None` (decision 0009).
    /// </remarks>
    let private ofChildren (describe: string) (children: Value list) : Outcome<Table> =
        let rec check index (expected: string option) remaining : Outcome<Tag list> =
            match remaining with
            | [] -> Ok []
            | child :: rest ->
                match childTag child with
                | Option.None -> Error(Fault.notATable describe index (sprintf "is %s, not a tag" (Value.kind child)))
                | Some tag when not (List.isEmpty tag.Children) ->
                    Error(Fault.notATable describe index "has children of its own")
                | Some tag ->
                    match expected with
                    | Some wanted when tag.TypeName <> wanted ->
                        Error(
                            Fault.notATable
                                describe
                                index
                                (sprintf "is <%s> where the first is <%s>" tag.TypeName wanted))
                    | _ -> check (index + 1) (Some tag.TypeName) rest |> Outcome.map (fun rest -> tag :: rest)

        outcome {
            let! tags = check 1 Option.None children

            let columns =
                tags |> List.collect (fun tag -> Value.orderedAttributes tag |> List.map fst) |> List.distinct

            let rows =
                tags
                |> List.map (fun tag ->
                    columns
                    |> List.map (fun name ->
                        match Map.tryFind name tag.Attributes with
                        | Some value -> value
                        | Option.None -> Value.None))

            return ofColumns columns rows
        }

    /// Decision 0009's coercion: a tag is table-shaped when every child is a tag of the
    /// same type and no child has children of its own.
    let ofTag (tag: Tag) : Outcome<Table> = ofChildren ("<" + tag.TypeName + ">") tag.Children

    /// One row as a tag. Gaps are left out rather than written empty: an attribute that
    /// is not there is what a gap is.
    let rowTag (typeName: string) (table: Table) (row: Value list) : Tag =
        List.zip (names table) row
        |> List.filter (fun (_, value) -> not (Value.isAbsent value))
        |> fun pairs -> Tag.create typeName pairs []

    let row (table: Table) (cells: Value list) = Value.Object(rowTag rowType table cells)

    /// The inverse of `ofTag`: a parent tag whose children are the rows.
    let toTag (typeName: string) (table: Table) : Tag =
        Tag.create typeName [] (table.Rows |> List.map (row table))

    // ----------------------------------------------------------------- Coercion

    /// <summary>Whatever a table function was handed, as a table.</summary>
    /// <remarks>
    /// A table is itself; a table-shaped tag and a list of same-typed tags are read as
    /// one (decision 0009); anything else says what it is instead, because a message
    /// naming the value is more use than an empty table would be.
    /// </remarks>
    let ofValue (command: string) (value: Value) : Outcome<Table> =
        match value with
        | Value.Table table -> Ok table
        | Value.Object tag
        | Value.Component tag -> ofTag tag
        | Value.List [] -> Ok empty
        | Value.List items -> ofChildren "the list" items
        | other -> Error(Fault.needsATable command (Value.kind other))
