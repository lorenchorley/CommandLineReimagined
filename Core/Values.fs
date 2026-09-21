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
      Children: Value list }

/// <summary>
/// Everything a command can return or be given.
/// </summary>
/// <remarks>
/// Qualified access is required, because the `None` case would otherwise shadow
/// `Option.None` everywhere this namespace is opened, and absence is spelled with an
/// option often enough in here that the shadowing would be a standing trap.
///
/// `Table` and `Query` arrive in Phases 3 and 4, and `Fault` in Phase 5 with `try`.
/// Until a phase has a way to produce a case there is nothing that could return it.
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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Value =

    /// The parent entry `ls` puts at the head of a listing. It is a `File` whose kind
    /// says it navigates rather than names a target, which is what the desktop shell
    /// and the browser page have both always drawn as `up`.
    let parentKind = "parent"

    let folderKind = "folder"

    /// A number reads as a person would write it: no trailing zeros, no thousands
    /// separator, and the same on every machine.
    let formatNumber (n: float) = n.ToString("0.###", CultureInfo.InvariantCulture)

    /// The full path of a record given the folder it is in and its name. The root is
    /// `/` and has no trailing separator, so joining to it must not double the slash.
    let joinPath (folder: string) (name: string) =
        if folder = "/" then "/" + name
        else folder + "/" + name

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

    and private tagText (opening: string) (closing: string) (tag: Tag) =
        let attributes =
            if Map.isEmpty tag.Attributes then ""
            else
                " "
                + (tag.Attributes
                   |> Map.toList
                   |> List.map (fun (name, v) -> name + "=" + display v)
                   |> String.concat " ")

        if List.isEmpty tag.Children then
            opening + tag.TypeName + attributes + "/" + closing
        else
            opening + tag.TypeName + attributes + closing
            + (tag.Children |> List.map display |> String.concat "")
            + opening + "/" + tag.TypeName + closing

    /// <summary>What the value means when a command is given it as an argument.</summary>
    /// <remarks>
    /// The two-string rule: a file shows its name but argues its path, so `ls` reads
    /// as a list of names and `ls | cd` still lands somewhere. A parent entry argues
    /// the folder it points at, because that is the whole of what it is for.
    /// </remarks>
    let argument (value: Value) : string =
        match value with
        | Value.File file when file.Kind = parentKind -> file.Folder
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
        | Value.File file when file.Kind = parentKind -> parentKind
        | Value.File file when file.Kind = folderKind -> folderKind
        | Value.File _ -> "file"
        | Value.List _ -> "list"
        | Value.Object _ -> "object"
        | Value.Component _ -> "component"

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
