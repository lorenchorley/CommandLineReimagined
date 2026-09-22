/// CSV, as RFC 4180 writes it: a header row, then one record per line.
///
/// A CSV file is a table with the types taken off, so reading one has to put them back:
/// a column is a number column when every cell in it reads as a number, and text
/// otherwise, which is what makes `sort qty` numeric on a file that only ever held
/// characters. Like `Xml`, nothing here knows about files.
namespace CommandLineReimagined.Core

open System
open System.Text

[<RequireQualifiedAccess>]
module Csv =

    /// A field as it was written. Quoting matters after the fact: an unquoted empty
    /// field is a gap, and `""` is text that happens to be empty.
    type private Field = { Text: string; Quoted: bool }

    /// <summary>What `-delimiter` was given, as the character it means.</summary>
    /// <remarks>
    /// `tab` is accepted by name, because nobody can type a tab on a phone. A quote or
    /// a line break cannot delimit anything: both already mean something in a CSV.
    /// </remarks>
    let delimiter (written: string) : Outcome<char> =
        match written with
        | "tab" -> Ok '\t'
        | text when text.Length = 1 && text <> "\"" && text <> "\n" && text <> "\r" -> Ok text[0]
        | text -> Error(Fault.badDelimiter text)

    /// <summary>Splits content into records of fields, each with the line it began on.</summary>
    /// <remarks>
    /// A quoted field may hold the delimiter, a doubled quote and line breaks, so a
    /// record is not a line and the line a record began on is counted as it goes, for
    /// the message. `\n` and `\r\n` both end a record. The line break after the last
    /// record ends it rather than starting an empty one after it.
    /// </remarks>
    let private records (path: string) (separator: char) (content: string) : Outcome<(int * Field list) list> =
        let length = content.Length
        let index = ref 0
        let line = ref 1
        let fault = ref Option.None
        let found = ResizeArray<int * Field list>()

        /// After a field: another field, the end of the record, or the end of the text.
        /// Answers whether the record goes on.
        let afterField () =
            if index.Value >= length then
                false
            elif content[index.Value] = separator then
                index.Value <- index.Value + 1
                true
            elif content[index.Value] = '\r' && index.Value + 1 < length && content[index.Value + 1] = '\n' then
                index.Value <- index.Value + 2
                line.Value <- line.Value + 1
                false
            elif content[index.Value] = '\n' then
                index.Value <- index.Value + 1
                line.Value <- line.Value + 1
                false
            else
                fault.Value <- Some(Fault.csvTextAfterQuote path line.Value)
                false

        while index.Value < length && fault.Value.IsNone do
            let start = line.Value
            let fields = ResizeArray<Field>()
            let mutable more = true

            while more && fault.Value.IsNone do
                let text = StringBuilder()

                if index.Value < length && content[index.Value] = '"' then
                    index.Value <- index.Value + 1
                    let mutable closed = false

                    while not closed && index.Value < length do
                        match content[index.Value] with
                        | '"' when index.Value + 1 < length && content[index.Value + 1] = '"' ->
                            text.Append '"' |> ignore
                            index.Value <- index.Value + 2
                        | '"' ->
                            closed <- true
                            index.Value <- index.Value + 1
                        | c ->
                            if c = '\n' then line.Value <- line.Value + 1
                            text.Append c |> ignore
                            index.Value <- index.Value + 1

                    if not closed then
                        fault.Value <- Some(Fault.csvUnclosedQuote path start)
                    else
                        fields.Add { Text = text.ToString(); Quoted = true }
                        more <- afterField ()
                else
                    while index.Value < length
                          && content[index.Value] <> separator
                          && content[index.Value] <> '\n'
                          && not (content[index.Value] = '\r' && index.Value + 1 < length && content[index.Value + 1] = '\n') do
                        text.Append content[index.Value] |> ignore
                        index.Value <- index.Value + 1

                    fields.Add { Text = text.ToString(); Quoted = false }
                    // Only a separator or a line end can stop an unquoted field, so what
                    // follows it is never text.
                    more <- afterField ()

            found.Add(start, List.ofSeq fields)

        match fault.Value with
        | Some fault -> Error fault
        | Option.None -> Ok(List.ofSeq found)

    let private isBlank (fields: Field list) =
        match fields with
        | [ { Text = ""; Quoted = false } ] -> true
        | _ -> false

    /// <summary>The header's names, checked.</summary>
    /// <remarks>
    /// Two columns with one name would make `$row.qty` mean whichever came first, and
    /// the lookup ignores case, so `Qty` and `qty` count as one name.
    /// </remarks>
    let private headerNames (path: string) (fields: Field list) : Outcome<string list> =
        let rec check index seen remaining =
            match remaining with
            | [] -> Ok()
            | (field: Field) :: rest ->
                if field.Text = "" then
                    Error(Fault.csvUnnamedColumn path index)
                elif Set.contains (field.Text.ToLowerInvariant()) seen then
                    Error(Fault.csvDuplicateColumn path field.Text)
                else
                    check (index + 1) (Set.add (field.Text.ToLowerInvariant()) seen) rest

        check 1 Set.empty fields |> Outcome.map (fun () -> fields |> List.map (fun field -> field.Text))

    /// <summary>One column's cells, typed.</summary>
    /// <remarks>
    /// Number when every cell that is there reads as one, by the same rule a bare word
    /// is read by; text otherwise, including the cells that looked like numbers, so a
    /// column never holds both. A gap is a gap either way.
    /// </remarks>
    let private typeColumn (cells: Field list) : Value list =
        let present = cells |> List.filter (fun cell -> cell.Quoted || cell.Text <> "")

        let numeric =
            not (List.isEmpty present)
            && present
               |> List.forall (fun cell ->
                   match Value.ofWord cell.Text with
                   | Value.Number _ -> true
                   | _ -> false)

        cells
        |> List.map (fun cell ->
            if not cell.Quoted && cell.Text = "" then Value.None
            elif numeric then Value.ofWord cell.Text
            else Value.Text cell.Text)

    /// <summary>Reads a CSV file's content as a table.</summary>
    /// <remarks>
    /// Every record must be as wide as the header, because a table has one width and
    /// guessing which cell is missing would be guessing. A blank line is skipped when
    /// the header has more than one column, since it cannot be a record of that width;
    /// in a one-column file it is a record with a gap in it, which is what writing one
    /// out produces.
    /// </remarks>
    let read (path: string) (separator: char) (content: string) : Outcome<Table> =
        outcome {
            let! found = records path separator content

            match found with
            | [] -> return Table.empty
            | (_, header) :: rest ->
                let! names = headerNames path header
                let width = List.length names

                let body = rest |> List.filter (fun (_, fields) -> not (width > 1 && isBlank fields))

                let! rows =
                    body
                    |> Outcome.traverse (fun (line, fields) ->
                        let count = List.length fields
                        if count = width then Ok fields else Error(Fault.csvFieldCount path line count width))

                let columns =
                    List.init width (fun column -> rows |> List.map (List.item column) |> typeColumn)

                let cells = List.init (List.length rows) (fun row -> columns |> List.map (List.item row))

                return Table.ofColumns names cells
        }

    /// <summary>A table as CSV.</summary>
    /// <remarks>
    /// A header row, then a record per row, every line ending in `\n` including the last.
    /// A field is quoted only when it has to be: when it holds the delimiter, a quote or
    /// a line break, or when it is empty text, which unquoted would read back as a gap.
    /// A table with no columns has no header to write, and is the empty file.
    /// </remarks>
    let write (separator: char) (table: Table) : string =
        let field (text: string) =
            if text = ""
               || text.Contains separator
               || text.Contains '"'
               || text.Contains '\n'
               || text.Contains '\r' then
                "\"" + text.Replace("\"", "\"\"") + "\""
            else
                text

        let cell (value: Value) =
            if Value.isAbsent value then "" else field (Value.dataText value)

        let line (fields: string list) = String.Join(string separator, fields) + "\n"

        if List.isEmpty table.Columns then
            ""
        else
            (line (Table.names table |> List.map field))
            + (table.Rows |> List.map (List.map cell >> line) |> String.concat "")
