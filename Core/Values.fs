/// The values a command can produce.
///
/// This replaces `Terminal.Execution.RuntimeValue`, the C# record hierarchy. The
/// difference that matters is not the language: it is that `File` names a record in
/// the store rather than a path on a disk, so a value can outlive the thing it points
/// at and still say what it was.
namespace CommandLineReimagined.Core

open System
open System.Globalization

/// A record's identity. A GUID in lower case with no braces, so it reads the same in
/// the log, in a projection and in JSON.
type FileId = string

/// A reference to a record in the store, as a value.
///
/// It carries a copy of the three attributes a value needs to describe itself rather
/// than a pointer into a projection, because a pipeline threads values across stages
/// and a later stage's projection is not the one the value was made in.
type FileRef =
    { Id: FileId
      Name: string
      Kind: string
      Folder: string }

/// A structured value: `<type a=1>...</type>` or `{type a=1/}`.
type Tag =
    { TypeName: string
      Attributes: Map<string, Value>
      /// <summary>The attribute names in the order they were written.</summary>
      /// <remarks>
      /// A map is sorted by key, and a tag is not: `<note name=monday mood=good/>` has
      /// to read back the way it was typed, and a row built from a table has to read in
      /// column order rather than alphabetically. The map is still what a lookup uses;
      /// this says what order to write them in, and a name missing from it falls back to
      /// the map's own order, so a tag built without one is not wrong, only alphabetical.
      /// </remarks>
      Order: string list
      Children: Value list }

/// <summary>What a table's column holds.</summary>
/// <remarks>
/// Decision 0009: a column is typed when every cell that is not `None` agrees, and is
/// `MixedCol` when they do not. The type is what decides whether `lt` compares numbers
/// or text and whether `sort` orders numerically, so getting it from the data rather
/// than from a declaration is what makes `sort qty` do the right thing on a table that
/// was read out of an XML file.
/// </remarks>
and ColumnType =
    | TextCol
    | NumberCol
    | BooleanCol
    | FileCol
    | ObjectCol
    | MixedCol

and Column = { Name: string; Type: ColumnType }

/// <summary>Rows and columns, as a value.</summary>
/// <remarks>
/// Every row has exactly `Columns.Length` cells; a gap is `Value.None` rather than a
/// short row, so a cell can always be addressed by its column's index.
/// </remarks>
and Table =
    { Columns: Column list
      Rows: Value list list }

/// <summary>An expression: a predicate, or one operand of one.</summary>
/// <remarks>
/// Declared here rather than beside its module because `Value.Query` names one and
/// values compile first, which is the same reason `FileId` is declared here.
///
/// This is the evaluated form: what the parser produced, with the shape of the tree
/// kept and the parser's node types left behind. `Nested` is the exception, because a
/// pipeline can only be run by the evaluator and there is nothing useful to translate
/// it into until Phase 5 gives it a meaning.
/// </remarks>
and [<RequireQualifiedAccess>] Expr =
    | Const of Value
    /// `$row.size`: a variable and the members read off it, in order.
    | Variable of name: string * members: string list
    /// One of `eq ne gt ge lt le like has`.
    | Compare of op: string * left: Expr * right: Expr
    | And of Expr * Expr
    | Or of Expr * Expr
    | Not of Expr
    /// A pipeline written in parentheses, still as the parser produced it.
    | Nested of Commands.Parser.SemanticTree.PipedCommandList

/// <summary>
/// Everything a command can return or be given.
/// </summary>
/// <remarks>
/// Qualified access is required, because the `None` case would otherwise shadow
/// `Option.None` everywhere this namespace is opened, and absence is spelled with an
/// option often enough in here that the shadowing would be a standing trap.
///
/// `Fault` is Phase 5's: `try` turns a failure into one, and `else` hands one to the
/// pipeline that recovers. It is an ordinary value from then on, so a variable can
/// hold it and `$problem.kind` can read it.
/// </remarks>
and [<RequireQualifiedAccess>] Value =
    /// A command that returns nothing. Distinct from `None`: this is "no answer",
    /// `None` is "the answer is that there is nothing".
    | Empty
    | None
    | Text of string
    | Number of float
    | Boolean of bool
    | File of FileRef
    | List of Value list
    | Object of Tag
    | Component of Tag
    | Table of Table
    /// A predicate as a value. It is what a `Predicate` parameter is handed, and from
    /// Phase 4 what a location's view is made of.
    | Query of Expr
    /// A failure, held as a value rather than stopping the line (decision 0014).
    | Fault of Fault

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Tag =

    /// <summary>A tag from its attributes, in the order they were given.</summary>
    /// <remarks>
    /// The only way a tag should be built: writing the record out by hand means
    /// remembering to keep `Order` and `Attributes` agreeing, and this is the one place
    /// that has to.
    /// </remarks>
    let create (typeName: string) (attributes: (string * Value) list) (children: Value list) =
        { TypeName = typeName
          Attributes = Map.ofList attributes
          Order = attributes |> List.map fst
          Children = children }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Value =

    let folderKind = "folder"

    /// A saved query, as a file (Phase 4). Its content is the predicate text, so a view
    /// is a record like any other and can be listed, renamed, undone and deleted.
    let viewKind = "view"

    /// A number reads as a person would write it: no trailing zeros, no thousands
    /// separator, and the same on every machine.
    let formatNumber (n: float) = n.ToString("0.###", CultureInfo.InvariantCulture)

    /// The full path of a record given the folder it is in and its name. The root is
    /// `/` and has no trailing separator, so joining to it must not double the slash.
    let joinPath (folder: string) (name: string) =
        if folder = "/" then "/" + name
        else folder + "/" + name

    /// <summary>A tag's attributes in the order they should be written.</summary>
    /// <remarks>
    /// `Order` first, for the names it knows, then anything the map has that it does
    /// not, so a tag built without an order is written alphabetically rather than
    /// incompletely.
    /// </remarks>
    let orderedAttributes (tag: Tag) : (string * Value) list =
        let named =
            tag.Order
            |> List.choose (fun name -> tag.Attributes |> Map.tryFind name |> Option.map (fun v -> name, v))

        let known = named |> List.map fst |> Set.ofList

        named
        @ (tag.Attributes |> Map.toList |> List.filter (fun (name, _) -> not (known.Contains name)))

    /// <summary>How the value reads to a person.</summary>
    let rec display (value: Value) : string =
        match value with
        | Value.Empty -> ""
        | Value.None -> ""
        | Value.Text text -> text
        | Value.Number n -> formatNumber n
        | Value.Boolean b -> if b then "true" else "false"
        | Value.File file -> file.Name
        | Value.List items -> items |> List.map display |> String.concat " "
        | Value.Object tag -> tagText "<" ">" tag
        | Value.Component tag -> tagText "{" "}" tag
        | Value.Table table -> tableText table
        | Value.Query expr -> exprText expr
        // A fault reads as the sentence it would have been on the red line. What makes
        // it a value is that the line went on.
        | Value.Fault fault -> fault.Message

    and private tagText (opening: string) (closing: string) (tag: Tag) =
        let attributes =
            if Map.isEmpty tag.Attributes then ""
            else
                " "
                // One line, like the notation. An attribute holding a nested table —
                // which is what a row from `group` carries — would otherwise print its
                // own rows down the page in the middle of a tag.
                + (orderedAttributes tag
                   |> List.map (fun (name, v) -> name + "=" + cellText v)
                   |> String.concat " ")

        if List.isEmpty tag.Children then
            opening + tag.TypeName + attributes + "/" + closing
        else
            opening + tag.TypeName + attributes + closing
            + (tag.Children |> List.map display |> String.concat "")
            + opening + "/" + tag.TypeName + closing

    /// <summary>A table, as aligned text.</summary>
    /// <remarks>
    /// The header, then a row per line, each column padded to the widest thing in it
    /// and two spaces between. This is what the desktop shell and a test read; the
    /// browser draws a real table from the same value. There is no trailing padding on
    /// the last column, so a line has no invisible spaces on the end of it.
    /// </remarks>
    and private tableText (table: Table) =
        let cells: string list list = table.Rows |> List.map (List.map cellText)
        let headers = table.Columns |> List.map (fun column -> column.Name)

        let widths =
            headers
            |> List.mapi (fun index header ->
                cells
                |> List.fold (fun widest row -> max widest (List.item index row).Length) header.Length)

        let line (row: string list) =
            List.zip row widths
            |> List.map (fun (text, width) -> text.PadRight width)
            |> String.concat "  "
            |> fun text -> text.TrimEnd()

        (headers :: cells) |> List.map line |> String.concat "\n"

    /// <summary>One cell, on one line.</summary>
    /// <remarks>
    /// A cell is one cell: a nested table — which is what `group` puts in one — would
    /// otherwise print its own rows down the page and take the alignment with it, so it
    /// says how many rows it has and `rows` is how you look inside. Text with a line
    /// break in it is folded for the same reason.
    /// </remarks>
    and private cellText (value: Value) =
        match value with
        | Value.Table nested ->
            let count = List.length nested.Rows
            sprintf "%d row%s" count (if count = 1 then "" else "s")
        | other -> (display other).Replace("\r\n", " ").Replace("\n", " ")

    /// <summary>An expression, written the way it was typed.</summary>
    /// <remarks>
    /// A query is a value, so it has to read back as something a person could type
    /// again — which is exactly what Phase 4 needs, since a location's view is shown in
    /// the prompt and `cd` on it has to mean the same thing twice.
    /// </remarks>
    and exprText (expr: Expr) : string =
        match expr with
        | Expr.Const value -> display value
        | Expr.Variable(name, members) -> "$" + name + (members |> List.map (fun m -> "." + m) |> String.concat "")
        | Expr.Compare(op, left, right) -> exprText left + " " + op + " " + exprText right
        | Expr.And(left, right) -> exprText left + " and " + exprText right
        | Expr.Or(left, right) -> exprText left + " or " + exprText right
        | Expr.Not operand -> "not " + exprText operand
        | Expr.Nested pipeline ->
            let visitor = Isagri.Reporting.Quid.RequestFilters.SemanticTree.SerialisationVisitor()
            pipeline.Accept visitor
            "(" + visitor.GetResult() + ")"

    /// <summary>What the value means when a command is given it as an argument.</summary>
    /// <remarks>
    /// The two-string rule: a file shows its name but argues its path, so `ls` reads
    /// as a list of names and `ls | cd` still lands somewhere.
    /// </remarks>
    let argument (value: Value) : string =
        match value with
        | Value.File file -> joinPath file.Folder file.Name
        | other -> display other

    /// The tag a DTO carries, so the page can draw a folder differently from a file.
    let kind (value: Value) : string =
        match value with
        | Value.Empty -> "empty"
        | Value.None -> "none"
        | Value.Text _ -> "text"
        | Value.Number _ -> "number"
        | Value.Boolean _ -> "boolean"
        | Value.File file when file.Kind = folderKind -> folderKind
        | Value.File _ -> "file"
        | Value.List _ -> "list"
        | Value.Object _ -> "object"
        | Value.Component _ -> "component"
        | Value.Table _ -> "table"
        | Value.Query _ -> "query"
        | Value.Fault _ -> "fault"

    /// <summary>
    /// A bare word that reads as a number becomes one; everything else stays text.
    /// </summary>
    /// <remarks>
    /// The grammar cannot tell `42` from `notes.txt`: both are words. Deciding here
    /// rather than in the parser keeps the tree honest about what was written, and is
    /// where `echo -5` turns into minus five now that the grammar lets the word through.
    /// </remarks>
    let ofWord (text: string) : Value =
        match Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture) with
        | true, number -> Value.Number number
        | _ -> Value.Text text

    /// Whether the value is one a command should treat as "nothing was given".
    let isAbsent (value: Value) =
        match value with
        | Value.Empty
        | Value.None -> true
        | _ -> false
