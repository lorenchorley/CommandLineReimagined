/// The file commands, over attribute records rather than a disk.
///
/// Every one of these is a pure function of the projection: it reads, it decides, and
/// it returns events. Nothing here creates, writes or deletes anything — the store
/// does that when the whole line has succeeded (decision 0015).
module CommandLineReimagined.Core.Commands.Files

open System
open CommandLineReimagined.Core

/// <summary>A fresh record identity.</summary>
/// <remarks>
/// Injectable, because a test that asserts on a projection needs the ids to be the
/// same twice, and because replaying a log must not invent new ones.
/// </remarks>
type IdSource = unit -> FileId

let guidSource: IdSource = fun () -> Guid.NewGuid().ToString().ToLowerInvariant()

let private timestamp (at: DateTimeOffset) = Value.Text(at.ToString "o")

/// The attributes every new record starts with.
let private baseAttributes name kind folder at =
    Map.ofList
        [ Attributes.name, Value.Text name
          Attributes.kind, Value.Text kind
          Attributes.folder, Value.Text folder
          Attributes.created, timestamp at
          Attributes.modified, timestamp at ]

let private touch (at: DateTimeOffset) (attributes: Map<string, Value>) =
    Map.add Attributes.modified (timestamp at) attributes

/// <summary>Sorts a folder's contents the way a listing reads.</summary>
/// <remarks>
/// Folders before files, each group by name, ordinal. This is now normative: the old
/// specification said the order "follows the filesystem", which meant it was whatever
/// the host happened to return and could not be tested.
/// </remarks>
let private listingOrder (records: FileRecord list) =
    let folders, files = records |> List.partition Record.isFolder

    (folders |> List.sortWith (fun a b -> String.CompareOrdinal(Record.name a, Record.name b)))
    @ (files |> List.sortWith (fun a b -> String.CompareOrdinal(Record.name a, Record.name b)))

// --------------------------------------------------------------------------- ls

let ls =
    { Spec =
        CommandSpec.create
            "ls"
            "List files and directories in a directory, the current directory by default"
            [ "show"; "list"; "dir" ]
            [ Parameter.optional "path" "Directory to list; defaults to the current one" ]
      Run =
        fun invocation ->
            async {
                let target =
                    if Invocation.given "path" invocation then
                        Invocation.text "path" invocation
                    else
                        invocation.Location.Folder

                match Files.resolveFolder invocation.Projection invocation.Location target with
                | Error fault -> return Error fault
                | Ok folder ->
                    let entries =
                        Files.inFolder invocation.Projection folder
                        |> listingOrder
                        |> List.map Record.toValue

                    // The parent entry navigates rather than naming a target, and there
                    // is nowhere to go from the root, so it only appears below it.
                    let parent =
                        if folder = Files.root then
                            []
                        else
                            let up, _ = Files.split folder

                            [ Value.File
                                  { Id = ""
                                    Name = "up"
                                    Kind = Value.parentKind
                                    Folder = up } ]

                    return Invocation.pure' (Value.List(parent @ entries))
            } }

// --------------------------------------------------------------------------- cd

let private moveTo (invocation: Invocation) (target: string) =
    match Files.resolveFolder invocation.Projection invocation.Location target with
    | Error fault -> Error fault
    | Ok folder ->
        let after = { invocation.Location with Folder = folder }

        if after = invocation.Location then
            // Already there: no event, so the line commits nothing and `undo` reaches
            // past it rather than having a no-op to take back.
            Invocation.pure' (Value.Text folder)
        else
            Invocation.withEvents
                (Value.Text folder)
                [ LocationChanged(invocation.Location, after) ]

let cd =
    { Spec =
        CommandSpec.create
            "cd"
            "Enter a directory"
            [ "move"; "navigate"; "directory"; "folder" ]
            [ Parameter.create "TargetPath" "The directory to enter" |> Parameter.piped ]
      Run = fun invocation -> async { return moveTo invocation (Invocation.text "TargetPath" invocation) } }

let up =
    { Spec = CommandSpec.create "up" "Move up one directory" [ "move"; "parent"; "back"; "navigate" ] []
      Run = fun invocation -> async { return moveTo invocation ".." } }

let pwd =
    { Spec =
        CommandSpec.create
            "pwd"
            "The current directory"
            [ "where"; "current"; "directory"; "path"; "location" ]
            []
      Run = fun invocation -> async { return Invocation.pure' (Value.Text invocation.Location.Folder) } }

// ------------------------------------------------------------------------ mkdir

let mkdir (newId: IdSource) (now: unit -> DateTimeOffset) =
    { Spec =
        CommandSpec.create
            "mkdir"
            "Create a directory"
            [ "create"; "make"; "directory"; "folder" ]
            [ Parameter.create "FolderName" "The name of the directory to create" ]
      Run =
        fun invocation ->
            async {
                let written = Invocation.text "FolderName" invocation
                let absolute = Files.normalise invocation.Location.Folder written
                let parent, name = Files.split absolute

                if not (Files.folderExists invocation.Projection parent) then
                    return Error(Fault.directoryDoesNotExist parent)
                elif (Files.tryFindIn invocation.Projection parent name).IsSome then
                    return Error(Fault.targetDirectoryExists absolute)
                else
                    let record =
                        { Id = newId ()
                          Attributes = baseAttributes name Value.folderKind parent (now ())
                          Content = None }

                    return Invocation.withEvents (Record.toValue record) [ FileCreated record ]
            } }

// ------------------------------------------------------------------------ write

let write (newId: IdSource) (now: unit -> DateTimeOffset) =
    { Spec =
        CommandSpec.create
            "write"
            "Write text to a file, replacing its contents"
            [ "write"; "save"; "file"; "create"; "text" ]
            [ Parameter.create "path" "The file to write"
              Parameter.create "text" "What to write" |> Parameter.piped ]
      Run =
        fun invocation ->
            async {
                let written = Invocation.text "path" invocation
                let absolute = Files.normalise invocation.Location.Folder written
                let folder, name = Files.split absolute
                let text = Invocation.value "text" invocation |> Value.display

                if not (Files.folderExists invocation.Projection folder) then
                    return Error(Fault.directoryDoesNotExist folder)
                else
                    // The content goes to the blob store first and the event carries
                    // only hashes, so keeping the previous version costs nothing.
                    let! hash = invocation.Blobs.Put text

                    match Files.tryFindIn invocation.Projection folder name with
                    | Some existing when Record.isFolder existing -> return Error(Fault.isADirectory absolute)
                    | Some existing ->
                        let events =
                            [ ContentChanged(existing.Id, existing.Content, Some hash)
                              AttributesChanged(existing.Id, existing.Attributes, touch (now ()) existing.Attributes) ]

                        return Invocation.withEvents (Record.toValue existing) events
                    | None ->
                        let record =
                            { Id = newId ()
                              Attributes = baseAttributes name (Files.inferKind name) folder (now ())
                              Content = None }

                        return
                            Invocation.withEvents
                                (Record.toValue { record with Content = Some hash })
                                [ FileCreated record; ContentChanged(record.Id, None, Some hash) ]
            } }

// -------------------------------------------------------------------------- cat

let cat =
    { Spec =
        CommandSpec.create
            "cat"
            "Read a file and return its text"
            [ "read"; "print"; "show"; "file"; "contents"; "type" ]
            [ Parameter.create "path" "The file to read" |> Parameter.piped ]
      Run =
        fun invocation ->
            async {
                let written = Invocation.text "path" invocation
                let absolute = Files.normalise invocation.Location.Folder written

                match Files.resolve invocation.Projection invocation.Location written with
                | Error fault -> return Error fault
                | Ok record when Record.isFolder record -> return Error(Fault.isADirectory absolute)
                | Ok record ->
                    match record.Content with
                    // A file that has never been written has no blob. That is empty
                    // text, not a missing file: `save` makes records with attributes
                    // and no content at all.
                    | None -> return Invocation.pure' (Value.Text "")
                    | Some hash ->
                        let! content = invocation.Blobs.Get hash
                        return Invocation.pure' (Value.Text(defaultArg content ""))
            } }

// --------------------------------------------------------------------------- rm

let rm =
    { Spec =
        CommandSpec.create
            "rm"
            "Delete a file or an empty directory"
            [ "remove"; "delete"; "erase"; "file"; "directory" ]
            [ Parameter.create "path" "What to delete" |> Parameter.piped ]
      Run =
        fun invocation ->
            async {
                let written = Invocation.text "path" invocation
                let absolute = Files.normalise invocation.Location.Folder written

                match Files.resolve invocation.Projection invocation.Location written with
                | Error _ when absolute = Files.root -> return Error(Fault.cannotDeleteCurrentDirectory ())
                | Error _ -> return Error(Fault.nothingExistsAt absolute)
                | Ok record when Record.isFolder record ->
                    let path = Files.pathOf invocation.Projection record

                    // Refusing a folder with anything in it is deliberate: undoing a
                    // recursive delete means restoring a whole tree, and a shell that
                    // deletes trees on one word is not one to try out on a phone.
                    if not (List.isEmpty (Files.inFolder invocation.Projection path)) then
                        return Error(Fault.directoryNotEmpty path)
                    elif Files.isAncestorOf path invocation.Location.Folder then
                        return Error(Fault.cannotDeleteCurrentDirectory ())
                    else
                        return Invocation.withEvents (Value.Text($"Removed {Record.name record}")) [ FileDeleted record ]
                | Ok record ->
                    return Invocation.withEvents (Value.Text($"Removed {Record.name record}")) [ FileDeleted record ]
            } }

// --------------------------------------------------------------------------- cp

let cp (newId: IdSource) (now: unit -> DateTimeOffset) =
    { Spec =
        CommandSpec.create
            "cp"
            "Copy a file into a directory"
            [ "copy"; "duplicate"; "file" ]
            [ Parameter.create "sourcePathAndFile" "The file to copy"
              Parameter.create "targetPath" "The directory to copy it into" ]
      Run =
        fun invocation ->
            async {
                let sourceWritten = Invocation.text "sourcePathAndFile" invocation
                let targetWritten = Invocation.text "targetPath" invocation

                match Files.resolve invocation.Projection invocation.Location sourceWritten with
                | Error fault -> return Error fault
                | Ok source ->
                    match Files.resolveFolder invocation.Projection invocation.Location targetWritten with
                    | Error _ ->
                        return
                            Error(
                                Fault.targetDirectoryDoesNotExist (
                                    Files.normalise invocation.Location.Folder targetWritten
                                )
                            )
                    | Ok folder ->
                        let name = Record.name source

                        match Files.tryFindIn invocation.Projection folder name with
                        | Some _ -> return Error(Fault.targetFileExists (Value.joinPath folder name))
                        | None ->
                            // The copy shares the source's content hash, so copying a
                            // large file costs one record and no content at all.
                            let attributes =
                                source.Attributes
                                |> Map.add Attributes.folder (Value.Text folder)
                                |> Map.add Attributes.created (timestamp (now ()))
                                |> touch (now ())

                            let record =
                                { Id = newId ()
                                  Attributes = attributes
                                  Content = source.Content }

                            return Invocation.withEvents (Record.toValue record) [ FileCreated record ]
            } }

// ------------------------------------------------------------------------- attr

/// `name` and `kind` may be set; the rest are the runtime's (decision 0013).
let private settableReserved = [ Attributes.name; Attributes.kind ]

let attr (now: unit -> DateTimeOffset) =
    { Spec =
        CommandSpec.create
            "attr"
            "Show a file's attributes, or set them with name=value"
            [ "attribute"; "tag"; "metadata"; "property"; "label" ]
            [ Parameter.create "path" "The file" |> Parameter.piped
              Parameter.assignments "assignments" "Attributes to set, written name=value" ]
      Run =
        fun invocation ->
            async {
                match Files.resolve invocation.Projection invocation.Location (Invocation.text "path" invocation) with
                | Error fault -> return Error fault
                | Ok record ->
                    if List.isEmpty invocation.Assignments then
                        // Reading. A table in Phase 3; a list of lines until then.
                        let lines =
                            record.Attributes
                            |> Map.toList
                            |> List.map (fun (name, value) -> Value.Text($"{name} = {Value.display value}"))

                        return Invocation.pure' (Value.List lines)
                    else
                        let invalid =
                            invocation.Assignments
                            |> List.map fst
                            |> List.tryFind (fun name ->
                                List.contains name Attributes.reserved
                                && not (List.contains name settableReserved))

                        match invalid with
                        | Some name ->
                            return
                                Error(
                                    Fault.create
                                        Invalid
                                        (sprintf "'%s' is set by the terminal and cannot be written." name)
                                )
                        | None ->
                            let renamed =
                                invocation.Assignments
                                |> List.tryPick (fun (name, value) ->
                                    if name = Attributes.name then Some(Value.display value) else None)

                            let clash =
                                match renamed with
                                | Some newName when newName <> Record.name record ->
                                    Files.tryFindIn invocation.Projection (Record.folder record) newName
                                    |> Option.map (fun _ ->
                                        Fault.nameAlreadyExists (Value.joinPath (Record.folder record) newName))
                                | _ -> None

                            match clash with
                            | Some fault -> return Error fault
                            | None ->
                                let after =
                                    invocation.Assignments
                                    |> List.fold (fun map (name, value) -> Map.add name value map) record.Attributes
                                    |> touch (now ())

                                let updated = { record with Attributes = after }

                                return
                                    Invocation.withEvents
                                        (Record.toValue updated)
                                        [ AttributesChanged(record.Id, record.Attributes, after) ]
            } }

// ------------------------------------------------------------------------- save

/// <summary>Turns an object tag into a file.</summary>
/// <remarks>
/// This is decision 0013's claim made executable: a tag with attributes *is* a file
/// record, so writing one down and saving it is the whole of creating a file with
/// metadata. The type name becomes the kind and the `name` attribute becomes the name.
/// </remarks>
let save (newId: IdSource) (now: unit -> DateTimeOffset) =
    { Spec =
        CommandSpec.create
            "save"
            "Create a file from a tag: its type is the kind, its name attribute the name"
            [ "save"; "store"; "record"; "create" ]
            [ Parameter.create "tag" "The tag to save" |> Parameter.piped ]
      Run =
        fun invocation ->
            async {
                match Invocation.value "tag" invocation with
                | Value.Object tag ->
                    match Map.tryFind Attributes.name tag.Attributes with
                    | None -> return Error(Fault.create Invalid "A saved tag needs a 'name' attribute.")
                    | Some nameValue ->
                        let name = Value.display nameValue
                        let folder = invocation.Location.Folder

                        if name = "" then
                            return Error(Fault.create Invalid "A saved tag needs a 'name' attribute.")
                        elif (Files.tryFindIn invocation.Projection folder name).IsSome then
                            return Error(Fault.targetFileExists (Value.joinPath folder name))
                        else
                            let attributes =
                                tag.Attributes
                                |> Map.fold
                                    (fun map key value ->
                                        if key = Attributes.name then map else Map.add key value map)
                                    (baseAttributes name tag.TypeName folder (now ()))

                            let record =
                                { Id = newId ()
                                  Attributes = attributes
                                  Content = None }

                            return Invocation.withEvents (Record.toValue record) [ FileCreated record ]
                | other ->
                    return
                        Error(
                            Fault.create
                                Invalid
                                (sprintf "'save' needs a tag, not %s." (Value.kind other))
                        )
            } }
