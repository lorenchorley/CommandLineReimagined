/// The table functions.
///
/// Every one of them takes its table from the pipe, coerces whatever it was handed
/// (decision 0009), and returns another table. None of them changes anything: a
/// pipeline of these is a question, and asking a question leaves no transaction behind.
module CommandLineReimagined.Core.Commands.Tables

open System
open CommandLineReimagined.Core

/// <summary>The table every one of these works on.</summary>
/// <remarks>
/// Declared last, after the function's own arguments, so that positional arguments fill
/// what the function asked for and the table arrives through the pipe. `sort name desc`
/// then reads as it looks, instead of binding `name` to the table.
/// </remarks>
let private tableParameter =
    Parameter.optional "table" "The table to work on; taken from the pipe when it is not written"
    |> Parameter.piped

let private tableOf (invocation: Invocation) =
    Table.ofValue invocation.Spec.Name (Invocation.value "table" invocation)

/// A table function: it reads a table and answers a value, and changes nothing.
let private pure' name description keywords parameters (run: Invocation -> Table -> Outcome<Value>) =
    // Read-only by construction: `pure'` is the shape of "asks a question", so every
    // table function a live view could re-run is marked here once (Phase 4).
    { Spec =
        CommandSpec.create name description keywords (parameters @ [ tableParameter ])
        |> CommandSpec.readOnly
      Run =
        fun invocation ->
            async { return tableOf invocation |> Outcome.bind (run invocation) |> Outcome.map (fun value -> { Value = value; Events = [] }) } }

/// The index of a column the user named, or a fault that names what is there instead.
let private columnIndex (invocation: Invocation) (table: Table) (name: string) =
    Table.indexOf table name
    |> Outcome.ofOption (Fault.noSuchColumn invocation.Spec.Name name)

// ------------------------------------------------------------------------ where

let where =
    pure'
        "where"
        "Keep the rows a predicate is true for"
        [ "filter"; "keep"; "select"; "match"; "query" ]
        [ Parameter.predicate "predicate" "An expression over $row, such as $row.kind eq folder" ]
        (fun invocation table ->
            match Invocation.predicate "predicate" invocation with
            | None -> Error(Fault.needsArgument "where" "predicate")
            | Some expr ->
                // A frame of its own per row, so `$row` is local to the predicate and
                // a variable of that name outside it is left alone (decision 0008).
                let keep (row: Value list) =
                    let scope = invocation.Scope.Push().Bind "row" (Table.row table row)
                    Expr.evaluate scope expr |> Outcome.map Expr.isTrue

                table.Rows
                |> Outcome.traverse (fun row -> keep row |> Outcome.map (fun kept -> row, kept))
                |> Outcome.map (fun judged ->
                    Value.Table
                        { table with Rows = judged |> List.filter snd |> List.map fst }))

// ----------------------------------------------------------------------- select

let select =
    pure'
        "select"
        "Keep only the named columns, in the order named"
        [ "columns"; "project"; "pick"; "only" ]
        [ Parameter.rest "columns" "The columns to keep" ]
        (fun invocation table ->
            match Invocation.list "columns" invocation |> List.map Value.display with
            | [] -> Error(Fault.create Binding "'select' needs at least one column.")
            | wanted ->
                wanted
                |> Outcome.traverse (columnIndex invocation table)
                |> Outcome.map (fun indexes ->
                    Value.Table
                        { Columns = indexes |> List.map (fun index -> List.item index table.Columns)
                          Rows = table.Rows |> List.map (fun row -> indexes |> List.map (fun i -> List.item i row)) }))

// ------------------------------------------------------------------------- sort

let sort =
    pure'
        "sort"
        "Order the rows by a column"
        [ "order"; "arrange"; "by" ]
        [ Parameter.create "column" "The column to order by"
          Parameter.optional "desc" "Write 'desc' to order downwards" |> Parameter.withFlag "desc" ]
        (fun invocation table ->
            outcome {
                let! index = columnIndex invocation table (Invocation.text "column" invocation)

                // F#'s sort is stable, so rows that compare equal keep the order they
                // arrived in and two sorts in a row compose the way a reader expects.
                // Descending negates the comparison rather than reversing the result,
                // which would put the ties back to front as well.
                let direction = if Invocation.flag "desc" invocation then -1 else 1

                let rows =
                    table.Rows
                    |> List.sortWith (fun a b -> direction * Expr.order (List.item index a) (List.item index b))

                return Value.Table { table with Rows = rows }
            })

// ------------------------------------------------------------------ take, skip

let private countOf (invocation: Invocation) =
    match Invocation.value "count" invocation with
    | Value.Number n when n >= 0.0 && Double.IsInteger n -> Ok(int n)
    | other -> Error(Fault.create Invalid (sprintf "'count' must be a whole number, not '%s'." (Value.display other)))

let take =
    pure'
        "take"
        "Keep the first rows"
        [ "first"; "head"; "limit"; "top" ]
        [ Parameter.create "count" "How many rows to keep" ]
        (fun invocation table ->
            countOf invocation
            |> Outcome.map (fun count -> Value.Table { table with Rows = table.Rows |> List.truncate count }))

let skip =
    pure'
        "skip"
        "Drop the first rows and keep the rest"
        [ "drop"; "rest"; "tail"; "after" ]
        [ Parameter.create "count" "How many rows to drop" ]
        (fun invocation table ->
            countOf invocation
            |> Outcome.map (fun count ->
                Value.Table
                    { table with
                        Rows = table.Rows |> List.skip (min count (List.length table.Rows)) }))

// ----------------------------------------------------------- first, last, count

/// <summary>A single row, or `None`.</summary>
/// <remarks>
/// `None` rather than a fault: "there are no rows" is an answer, not a failure, and it
/// is what `??` will default in Phase 5.
/// </remarks>
let private single name description keywords (pick: Value list list -> Value list option) =
    pure' name description keywords [] (fun _ table ->
        match pick table.Rows with
        | Some row -> Ok(Table.row table row)
        | None -> Ok Value.None)

let first = single "first" "The first row, or nothing" [ "head"; "top"; "one" ] List.tryHead

let last = single "last" "The last row, or nothing" [ "tail"; "bottom"; "end" ] List.tryLast

let count =
    pure' "count" "How many rows there are" [ "length"; "size"; "how"; "many"; "total" ] [] (fun _ table ->
        Ok(Value.Number(float (List.length table.Rows))))

// --------------------------------------------------------------------- distinct

let distinct =
    pure'
        "distinct"
        "Unique rows, or the unique values of one column"
        [ "unique"; "different"; "dedupe" ]
        [ Parameter.optional "column" "A column, to take the unique values of it" ]
        (fun invocation table ->
            if not (Invocation.given "column" invocation) then
                Ok(Value.Table { table with Rows = table.Rows |> List.distinctBy (List.map Value.display) })
            else
                outcome {
                    let name = Invocation.text "column" invocation
                    let! index = columnIndex invocation table name

                    let values =
                        table.Rows
                        |> List.map (fun row -> [ List.item index row ])
                        |> List.distinctBy (List.map Value.display)

                    return Value.Table(Table.ofColumns [ name ] values)
                })

// ------------------------------------------------------------------------ group

let group =
    pure'
        "group"
        "Gather the rows that share a column's value"
        [ "by"; "gather"; "bucket"; "collect" ]
        [ Parameter.create "column" "The column to group by" ]
        (fun invocation table ->
            outcome {
                let! index = columnIndex invocation table (Invocation.text "column" invocation)

                // Grouped on the display text rather than the value, so a column that
                // holds a number and the text of that number groups as one thing, the
                // same way `eq` treats them.
                let groups =
                    table.Rows
                    |> List.groupBy (fun row -> Value.display (List.item index row))
                    |> List.map (fun (_, rows) ->
                        [ List.item index (List.head rows); Value.Table { table with Rows = rows } ])

                return Value.Table(Table.ofColumns [ "key"; "rows" ] groups)
            })

// -------------------------------------------------------------- columns, rows

let columns =
    pure' "columns" "The table's columns and their types" [ "headers"; "fields"; "schema" ] [] (fun _ table ->
        let named =
            table.Columns
            |> List.map (fun column ->
                [ Value.Text column.Name
                  Value.Text(
                      match column.Type with
                      | TextCol -> "text"
                      | NumberCol -> "number"
                      | BooleanCol -> "boolean"
                      | FileCol -> "file"
                      | ObjectCol -> "object"
                      | MixedCol -> "mixed") ])

        Ok(Value.Table(Table.ofColumns [ "name"; "type" ] named)))

let rows =
    pure' "rows" "The rows, as objects rather than a table" [ "items"; "records"; "objects" ] [] (fun _ table ->
        Ok(Value.List(table.Rows |> List.map (Table.row table))))

// ------------------------------------------------------------------------ table

/// <summary>Coercion, written out.</summary>
/// <remarks>
/// Decision 0009 makes the coercion implicit wherever a table is expected, so this is
/// for the case where nothing expects one: seeing what a tag reads as, and saying in a
/// script that a value is meant to be a table.
/// </remarks>
let table =
    pure' "table" "Read a tag or a list of tags as a table" [ "rows"; "grid"; "convert"; "as" ] [] (fun _ table ->
        Ok(Value.Table table))

/// Every table function, in the order `help` lists them.
let all =
    [ columns; count; distinct; first; group; last; rows; select; skip; sort; table; take; where ]
