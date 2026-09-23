/// XML and CSV files, read as the values the language already has and written back.
///
/// Decision 0011: a document is a real file in the store. The readers are `cat` with a
/// parser behind it, and change nothing; the writers are `write` with a serialiser in
/// front of it, and emit exactly the events `write` does, so undo, redo and history
/// treat a document like any other file.
module CommandLineReimagined.Core.Commands.Documents

open System
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Commands.Files

let private pathParameter description =
    Parameter.create "path" description |> Parameter.piped |> Parameter.takes Takes.Path

/// A reader: the file's text, handed to a parser that knows nothing about files.
let private reader name description keywords extra (parse: Invocation -> string -> string -> Outcome<Value>) =
    { Spec =
        CommandSpec.create name description keywords (pathParameter "The file to read" :: extra)
        |> CommandSpec.readOnly
      Run =
        fun invocation ->
            async {
                let! content = readContent invocation (Invocation.text "path" invocation)

                return
                    content
                    |> Outcome.bind (fun (path, text) -> parse invocation path text)
                    |> Outcome.map (fun value -> { Value = value; Events = [] })
            } }

/// <summary>A writer: something serialised, then written the way `write` writes.</summary>
/// <remarks>
/// The path comes first and the value is piped, the same shape as `write`, so
/// `... | to-csv reorder.csv` reads the way it looks. The value is written or piped;
/// the name options come last, where only a flag reaches them in practice.
/// </remarks>
let private writer
    (newId: IdSource)
    (now: unit -> DateTimeOffset)
    name
    description
    keywords
    kind
    valueDescription
    extra
    (serialise: Invocation -> Value -> Outcome<string>)
    =
    { Spec =
        CommandSpec.create
            name
            description
            keywords
            ([ Parameter.create "path" "The file to write" |> Parameter.takes Takes.Path
               Parameter.create "value" valueDescription |> Parameter.piped |> Parameter.takes Takes.Value ]
             @ extra)
      Run =
        fun invocation ->
            async {
                match serialise invocation (Invocation.value "value" invocation) with
                | Error fault -> return Error fault
                | Ok text -> return! writeContent newId now invocation (Invocation.text "path" invocation) (Some kind) text
            } }

/// A text option, or nothing when it was not written.
let private option (name: string) (invocation: Invocation) =
    if Invocation.given name invocation then Some(Invocation.text name invocation) else None

let private delimiterParameter =
    Parameter.withDefault (Value.Text ",") (Parameter.create "delimiter" "The character between fields; 'tab' for a tab")
    |> Parameter.takes Takes.Text

let private delimiterOf (invocation: Invocation) = Csv.delimiter (Invocation.textOr "," "delimiter" invocation)

// ---------------------------------------------------------------------- from-xml

let fromXml =
    reader
        "from-xml"
        "Read an XML file as a tag, which reads as a table when it is table-shaped"
        [ "xml"; "read"; "parse"; "load"; "import"; "document" ]
        []
        (fun _ path text -> Xml.read path text |> Outcome.map Value.Object)

// ------------------------------------------------------------------------ to-xml

let toXml (newId: IdSource) (now: unit -> DateTimeOffset) =
    writer
        newId
        now
        "to-xml"
        "Write a tag, a table or a list of tags to a file as XML"
        [ "xml"; "write"; "save"; "export"; "document" ]
        "xml"
        "The tag, table or list to write"
        [ Parameter.optional "root" "The root element's name" |> Parameter.takes Takes.Text
          Parameter.optional "row" "The name of a table's row elements; 'row' by default" |> Parameter.takes Takes.Text
          Parameter.optional "declaration" "Write '-declaration' to begin with an XML declaration"
          |> Parameter.takes (Takes.Switch("declaration", None)) ]
        (fun invocation value ->
            Invocation.flag "declaration" invocation
            |> Outcome.bind (fun declaration ->
                Xml.write
                    { Root = option "root" invocation
                      Row = option "row" invocation
                      Declaration = declaration }
                    value))

// ---------------------------------------------------------------------- from-csv

let fromCsv =
    reader
        "from-csv"
        "Read a CSV file with a header row as a table"
        [ "csv"; "read"; "parse"; "load"; "import"; "spreadsheet" ]
        [ delimiterParameter ]
        (fun invocation path text ->
            delimiterOf invocation
            |> Outcome.bind (fun separator -> Csv.read path separator text)
            |> Outcome.map Value.Table)

// ------------------------------------------------------------------------ to-csv

let toCsv (newId: IdSource) (now: unit -> DateTimeOffset) =
    writer
        newId
        now
        "to-csv"
        "Write a table to a file as CSV, with a header row"
        [ "csv"; "write"; "save"; "export"; "spreadsheet" ]
        "csv"
        "The table to write"
        [ delimiterParameter ]
        (fun invocation value ->
            outcome {
                let! separator = delimiterOf invocation
                let! table = Table.ofValue "to-csv" value
                return Csv.write separator table
            })

/// Every document command, in the order `help` lists them.
let all newId now = [ fromCsv; fromXml; toCsv newId now; toXml newId now ]
