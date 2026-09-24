/// The fold: what the log adds up to.
///
/// The filesystem, the variables and the current location are not state that commands
/// mutate. They are this, computed from the events, and a fresh store replaying the
/// same log arrives at exactly the same projection.
namespace CommandLineReimagined.Core

open System

type Projection =
    { Files: Map<FileId, FileRecord>
      Variables: Map<string, Value>
      Location: Location
      /// <summary>The places `in` and `out` have left, the most recent first (decision 0037).</summary>
      /// <remarks>
      /// What `back` retraces, one step at a time. A fold over the log like the rest,
      /// so it survives a reload, and `undo` puts a place back on it or takes one off.
      /// </remarks>
      Trail: Location list
      /// The sequence number of the last transaction folded in. A replay is finished
      /// when this equals the log's last sequence number.
      Applied: int64 }

[<RequireQualifiedAccess>]
module Attributes =

    /// The attributes the runtime owns. A user may set `name` and `kind`, which are
    /// validated; the rest are the runtime's to write (decision 0013).
    let name = "name"
    let kind = "kind"
    let folder = "folder"
    let created = "created"
    let modified = "modified"

    let reserved = [ name; kind; folder; created; modified ]

    /// `size` is deliberately absent from `reserved`: it is not stored at all but is the
    /// content's length, computed when it is asked for, so it can never disagree with
    /// the content (decision 0013). It is named so that nothing can store one.
    let size = "size"

    /// <summary>Every name a listing already has a column for, or the runtime owns.</summary>
    /// <remarks>
    /// A user attribute by one of these names would be a second column of the same name,
    /// so a listing's extra columns and `attr`'s refusals both start from this.
    /// </remarks>
    let owned = reserved @ [ size ]

    let text (attributes: Map<string, Value>) (key: string) =
        match Map.tryFind key attributes with
        | Some value -> Value.display value
        | Option.None -> ""

[<RequireQualifiedAccess>]
module Record =

    let name (record: FileRecord) = Attributes.text record.Attributes Attributes.name
    let kind (record: FileRecord) = Attributes.text record.Attributes Attributes.kind
    let folder (record: FileRecord) = Attributes.text record.Attributes Attributes.folder

    let isFolder (record: FileRecord) = kind record = Value.folderKind

    /// The value a pipeline threads when a command returns a record.
    let toRef (record: FileRecord) : FileRef =
        { Id = record.Id
          Name = name record
          Kind = kind record
          Folder = folder record }

    let toValue (record: FileRecord) = Value.File(toRef record)

[<RequireQualifiedAccess>]
module Projection =

    let emptyLocation = { Folder = "/"; View = Option.None }

    let empty =
        { Files = Map.empty
          Variables = Map.empty
          Location = emptyLocation
          Trail = []
          Applied = 0L }

    /// <summary>Whether two locations are the same place.</summary>
    /// <remarks>
    /// A view is compared by its text, the way it is stored: a view read back from the
    /// log is parsed again, and a nested pipeline in it would not compare equal to the
    /// one it was parsed from.
    /// </remarks>
    let samePlace (a: Location) (b: Location) =
        a.Folder = b.Folder && Option.map Value.exprText a.View = Option.map Value.exprText b.View

    /// Takes a place off the trail: the most recent time it is there. Forgiving, like
    /// the rest of the fold: a place that is not on the trail changes nothing.
    let private pop (place: Location) (trail: Location list) =
        match List.tryFindIndex (samePlace place) trail with
        | Some index -> List.removeAt index trail
        | Option.None -> trail

    /// <summary>Folds one event in.</summary>
    /// <remarks>
    /// Total, and deliberately forgiving: an event naming a record that is not there
    /// is ignored rather than failing. Validation happens before an event is appended,
    /// so by the time anything is folded the question is already settled, and a replay
    /// that could fail would mean a log that could not be read back.
    /// </remarks>
    let apply (projection: Projection) (event: Event) : Projection =
        match event with
        | FileCreated record -> { projection with Files = Map.add record.Id record projection.Files }
        | FileDeleted record -> { projection with Files = Map.remove record.Id projection.Files }
        | AttributesChanged(id, _, after) ->
            match Map.tryFind id projection.Files with
            | Some record ->
                { projection with Files = Map.add id { record with Attributes = after } projection.Files }
            | Option.None -> projection
        | ContentChanged(id, _, after) ->
            match Map.tryFind id projection.Files with
            | Some record ->
                { projection with Files = Map.add id { record with Content = after } projection.Files }
            | Option.None -> projection
        | VariableChanged(name, _, after) ->
            match after with
            | Some value -> { projection with Variables = Map.add name value projection.Variables }
            | Option.None -> { projection with Variables = Map.remove name projection.Variables }
        | LocationChanged(_, after) -> { projection with Location = after }
        | TrailPushed place -> { projection with Trail = place :: projection.Trail }
        | TrailPopped place -> { projection with Trail = pop place projection.Trail }

    let applyAll (projection: Projection) (events: Event list) = List.fold apply projection events

    /// <summary>The event that undoes this one.</summary>
    /// <remarks>
    /// Total, and its own inverse: `invert (invert e) = e` for every case. That
    /// identity is what makes redo free, because redoing is compensating a
    /// compensation rather than a second mechanism.
    /// </remarks>
    let invert (event: Event) : Event =
        match event with
        | FileCreated record -> FileDeleted record
        | FileDeleted record -> FileCreated record
        | AttributesChanged(id, before, after) -> AttributesChanged(id, after, before)
        | ContentChanged(id, before, after) -> ContentChanged(id, after, before)
        | VariableChanged(name, before, after) -> VariableChanged(name, after, before)
        | LocationChanged(before, after) -> LocationChanged(after, before)
        | TrailPushed place -> TrailPopped place
        | TrailPopped place -> TrailPushed place

    /// The events that undo a whole transaction: each one inverted, in reverse order,
    /// because a later event may depend on an earlier one having happened.
    let invertAll (events: Event list) = events |> List.map invert |> List.rev

[<RequireQualifiedAccess>]
module Files =

    let root = "/"

    /// <summary>Makes a path absolute and canonical.</summary>
    /// <remarks>
    /// Pure, and over strings alone: nothing here asks whether a path exists. `.` and
    /// `..` are resolved textually, and `..` at the root stays at the root rather than
    /// failing, which is what makes `in ..` at the top a no-op instead of an error.
    /// </remarks>
    let normalise (baseFolder: string) (path: string) : string =
        let combined =
            if path.StartsWith "/" then path
            elif baseFolder = root then root + path
            else baseFolder + "/" + path

        let segments =
            combined.Split('/')
            |> Array.fold
                (fun acc segment ->
                    match segment with
                    | "" | "." -> acc
                    | ".." -> (match acc with | _ :: rest -> rest | [] -> [])
                    | name -> name :: acc)
                []
            |> List.rev

        if List.isEmpty segments then root else root + String.Join("/", segments)

    /// The folder part of an absolute path, and the name part.
    let split (path: string) =
        let path = if path = root then root else path.TrimEnd '/'

        match path.LastIndexOf '/' with
        | -1 -> root, path
        | 0 -> root, path.Substring 1
        | index -> path.Substring(0, index), path.Substring(index + 1)

    let pathOf (_: Projection) (record: FileRecord) =
        Value.joinPath (Record.folder record) (Record.name record)

    /// The records directly inside a folder, in no particular order. Callers that show
    /// them to a person sort; callers that look one up do not care.
    let inFolder (projection: Projection) (folder: string) =
        projection.Files
        |> Map.toList
        |> List.map snd
        |> List.filter (fun record -> Record.folder record = folder)

    let tryFindIn (projection: Projection) (folder: string) (name: string) =
        inFolder projection folder
        |> List.tryFind (fun record -> String.Equals(Record.name record, name, StringComparison.Ordinal))

    /// Whether a folder exists as somewhere you could be. The root always does, and is
    /// not a record (decision 0016).
    let folderExists (projection: Projection) (path: string) =
        path = root
        || (let folder, name = split path

            match tryFindIn projection folder name with
            | Some record -> Record.isFolder record
            | Option.None -> false)

    /// <summary>Why a name cannot be a record's name, if it cannot.</summary>
    /// <remarks>
    /// One rule for making a record and for renaming one, so that no route — `mkdir`,
    /// `write`, `save`, `save-view` or `attr name=` — can make a record that has no
    /// path. `mkdir` and `write` split what they are given at `/` before a name ever
    /// reaches here; `save` and `attr` take the name as a value, which is where a
    /// `/` could otherwise get in.
    /// </remarks>
    let nameFault (name: string) : Fault option =
        if name = "" then Some(Fault.fileNeedsAName ())
        elif name.Contains '/' then Some(Fault.notAValidFileName name "'/' separates directories")
        elif name = "." || name = ".." then Some(Fault.notAValidFileName name "it already names a directory")
        else Option.None

    /// <summary>The kind of a file, guessed from its name when nobody said.</summary>
    /// <remarks>
    /// A guess, and overridable: `attr x kind=note` wins. The list is short on purpose
    /// — a name the table does not know is `text`, not an error.
    /// </remarks>
    let inferKind (name: string) =
        let extension =
            match name.LastIndexOf '.' with
            | index when index > 0 && index < name.Length - 1 ->
                name.Substring(index + 1).ToLowerInvariant()
            | _ -> ""

        match extension with
        | "xml" -> "xml"
        | "csv" -> "csv"
        | "json" -> "json"
        | "clr" -> "script"
        | "md" -> "markdown"
        | _ -> "text"

    /// Resolves a path written by a user to a record. `NotFound` names the path as it
    /// was resolved, not as it was typed, because being somewhere else than you thought
    /// is the usual cause.
    let resolve (projection: Projection) (location: Location) (path: string) : Outcome<FileRecord> =
        let absolute = normalise location.Folder path
        let folder, name = split absolute

        if absolute = root then
            Error(Fault.isADirectory root)
        else
            match tryFindIn projection folder name with
            | Some record -> Ok record
            | Option.None -> Error(Fault.fileDoesNotExist absolute)

    /// Resolves a path that has to be a folder, and returns its path rather than a
    /// record, because the root is a folder and is not a record.
    let resolveFolder (projection: Projection) (location: Location) (path: string) : Outcome<string> =
        let absolute = normalise location.Folder path

        if folderExists projection absolute then
            Ok absolute
        else
            Error(Fault.directoryDoesNotExist path)

    /// Every folder at or under a path, used by `rm` to refuse a folder with anything
    /// in it and by `in` to know whether it has been deleted from under itself.
    let isAncestorOf (ancestor: string) (path: string) =
        path = ancestor || path.StartsWith(if ancestor = root then root else ancestor + "/")
