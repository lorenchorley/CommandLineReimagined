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

/// <summary>The length of each record's content, in characters.</summary>
/// <remarks>
/// `size` is not stored (decision 0013): it is the content's length, so it can never
/// disagree with the content. Reading it means reading the blobs, which is why a
/// listing is async and why `Table.ofRecords` is handed the measurement rather than
/// taking the whole log to look one up.
/// </remarks>
let private measure (blobs: IBlobs) (records: FileRecord list) =
    async {
        let mutable sizes = Map.empty

        for record in records do
            match record.Content with
            | Some hash ->
                let! content = blobs.Get hash
                sizes <- Map.add record.Id (float (defaultArg content "").Length) sizes
            | None -> ()

        return fun (record: FileRecord) -> defaultArg (Map.tryFind record.Id sizes) 0.0
    }

/// <summary>Everything in the store, which is the whole of what a view asks about.</summary>
/// <remarks>
/// A view is not a folder and is not scoped to one (decision 0013): `cd $row.tag eq
/// work` is a question about the terminal, not about where you happen to be standing,
/// so `ls` in one lists across folders and shows the `folder` column to say where each
/// row came from.
/// </remarks>
let private everything (projection: Projection) =
    projection.Files |> Map.toList |> List.map snd

/// <summary>The records a predicate is true of, as a listing.</summary>
/// <remarks>
/// The predicate is evaluated against a row of the same shape `ls` produces, so
/// `$row.kind`, `$row.folder` and any attribute a record carries all mean in a view
/// exactly what they mean after `ls |`. The matching records are then listed on their
/// own, rather than the whole store's table being filtered, so the columns are the
/// attributes of what came back instead of the attributes of everything.
/// </remarks>
let private listMatching (invocation: Invocation) (expr: Expr) =
    async {
        let records = everything invocation.Projection
        let! size = measure invocation.Blobs records
        let candidates = Table.ofRecords size records

        let judged =
            List.zip records candidates.Rows
            |> Outcome.traverse (fun (record, row) ->
                // A frame of its own per row, so `$row` is local to the predicate
                // (decision 0008).
                let scope = invocation.Scope.Push().Bind "row" (Table.row candidates row)
                Expr.test scope expr |> Outcome.map (fun kept -> record, kept))

        match judged with
        | Error fault -> return Error fault
        | Ok pairs ->
            let matched = pairs |> List.filter snd |> List.map fst |> listingOrder
            return Invocation.pure' (Value.Table(Table.ofRecords size matched))
    }

let ls =
    { Spec =
        CommandSpec.create
            "ls"
            "List files and directories in a directory, the current directory by default"
            [ "show"; "list"; "dir" ]
            [ Parameter.optional "path" "Directory to list; defaults to the current one" |> Parameter.takes Takes.Place ]
        |> CommandSpec.readOnly
      Run =
        fun invocation ->
            async {
                let written = Invocation.given "path" invocation

                match invocation.Location.View with
                // In a view, a listing is the view (decision 0013). Writing a path is
                // how you look somewhere else without leaving it.
                | Some expr when not written -> return! listMatching invocation expr
                | _ ->

                let target =
                    if written then
                        Invocation.text "path" invocation
                    else
                        invocation.Location.Folder

                match Files.resolveFolder invocation.Projection invocation.Location target with
                | Error fault -> return Error fault
                | Ok folder ->
                    let entries = Files.inFolder invocation.Projection folder |> listingOrder
                    let! size = measure invocation.Blobs entries

                    // From Phase 3 there is no parent row: a table of records has
                    // nowhere to put one, and a row that navigates rather than naming a
                    // record would be a row with no record behind it. The page offers
                    // `up` in the location line instead.
                    return Invocation.pure' (Value.Table(Table.ofRecords size entries))
            } }

// ------------------------------------------------------------------------- find

let find =
    { Spec =
        CommandSpec.create
            "find"
            "List every record a predicate is true of, wherever it is"
            [ "search"; "query"; "look"; "everywhere" ]
            [ Parameter.predicate "predicate" "An expression over $row, such as $row.kind eq note" ]
        |> CommandSpec.readOnly
      Run =
        fun invocation ->
            async {
                match Invocation.predicate "predicate" invocation with
                | Option.None -> return Error(Fault.needsAPredicate "find")
                | Some expr when not (Expr.isPredicate expr) -> return Error(Fault.needsAPredicate "find")
                | Some expr ->
                    // Asking a question is not going anywhere: `find` leaves the
                    // location alone, which is the whole difference between it and `cd`.
                    return! listMatching invocation expr
            } }

// --------------------------------------------------------------------------- cd

/// <summary>A folder as the value `cd` answers with.</summary>
/// <remarks>
/// The record, so the answer is a chip you can tap and pipe like any other file. The
/// root is the exception: it is implicit and has no record (decision 0016), so there
/// is nothing to return but its path.
/// </remarks>
let private folderValue (projection: Projection) (folder: string) =
    if folder = Files.root then
        Value.Text folder
    else
        let parent, name = Files.split folder

        match Files.tryFindIn projection parent name with
        | Some record -> Record.toValue record
        | Option.None -> Value.Text folder

/// Moving somewhere clears any view: a folder and a view are two answers to the same
/// question, and holding both would leave `ls` with two things to list.
let private enterFolder (invocation: Invocation) (target: string) =
    match Files.resolveFolder invocation.Projection invocation.Location target with
    | Error fault -> Error fault
    | Ok folder ->
        let after = { Folder = folder; View = Option.None }
        let value = folderValue invocation.Projection folder

        if after = invocation.Location then
            // Already there: no event, so the line commits nothing and `undo` reaches
            // past it rather than having a no-op to take back.
            Invocation.pure' value
        else
            Invocation.withEvents value [ LocationChanged(invocation.Location, after) ]

/// <summary>Entering a query rather than a folder (decision 0013).</summary>
/// <remarks>
/// The folder is left as it was, because a view is a way of looking rather than a
/// place to put things: new files still land in `Folder`, and `up` puts the view down
/// and leaves you where you already were.
/// </remarks>
let private enterView (invocation: Invocation) (expr: Expr) =
    let after = { invocation.Location with View = Some expr }

    if after = invocation.Location then
        Invocation.pure' (Value.Query expr)
    else
        Invocation.withEvents (Value.Query expr) [ LocationChanged(invocation.Location, after) ]

/// <summary>Entering something named: a folder, or a saved view.</summary>
/// <remarks>
/// A view file is a place (decision 0013), so `cd weekend` on one enters the query it
/// holds rather than failing because it is not a directory. Anything else is a path,
/// and the failure is the same "Directory does not exist" it has always been.
/// </remarks>
let private enterNamed (invocation: Invocation) (written: string) =
    async {
        match Files.resolve invocation.Projection invocation.Location written with
        | Ok record when Record.kind record = Value.viewKind ->
            let path = Files.pathOf invocation.Projection record

            let! content =
                match record.Content with
                | Some hash -> invocation.Blobs.Get hash
                | Option.None -> async.Return(Some "")

            let text = defaultArg content ""

            match Expr.parse path text with
            | Error fault -> return Error fault
            // The check `save-view` makes before it writes one, made again on the way
            // back in: a view whose text is a plain word, because someone wrote over it
            // or retyped a note as a view, would otherwise be entered as a question
            // whose every answer is false.
            | Ok expr when not (Expr.isPredicate expr) -> return Error(Fault.notAPredicate path (text.Trim()))
            | Ok expr -> return enterView invocation expr
        | _ -> return enterFolder invocation written
    }

let cd =
    { Spec =
        CommandSpec.create
            "cd"
            "Enter a directory, a saved view, or a predicate written out"
            [ "move"; "navigate"; "directory"; "folder"; "view"; "query" ]
            [ Parameter.predicate "TargetPath" "The directory, view or predicate to enter"
              |> Parameter.piped
              |> Parameter.takes Takes.Place ]
      Run =
        fun invocation ->
            async {
                match Map.tryFind "TargetPath" invocation.Args with
                // Written with an operator in it: a question, and therefore a view.
                | Some(Value.Query expr) when Expr.isPredicate expr -> return enterView invocation expr
                // Written as a plain operand, or piped in as a value: a name.
                | Some(Value.Query expr) ->
                    match Expr.evaluate invocation.Scope expr with
                    | Error fault -> return Error fault
                    | Ok value -> return! enterNamed invocation (Value.argument value)
                | Some value -> return! enterNamed invocation (Value.argument value)
                | Option.None -> return Error(Fault.needsArgument "cd" "TargetPath")
            } }

/// Not read-only, for the same reason `cd` is not: it emits `LocationChanged`, so it
/// is in `history` and `undo` takes it back, and a live refresh must not run it.
let up =
    { Spec =
        CommandSpec.create "up" "Leave the current view, or move up one directory"
            [ "move"; "parent"; "back"; "navigate"; "view" ] []
      Run =
        fun invocation ->
            async {
                match invocation.Location.View with
                // A view is put down before the folder is left: two `up`s from a view
                // over a subfolder take you out of the question and then out of the
                // folder, which is the order they were entered in.
                | Some _ ->
                    let after = { invocation.Location with View = Option.None }

                    return
                        Invocation.withEvents
                            (Value.Text invocation.Location.Folder)
                            [ LocationChanged(invocation.Location, after) ]
                | Option.None -> return enterFolder invocation ".."
            } }

let pwd =
    { Spec =
        CommandSpec.create
            "pwd"
            "Where you are: the current directory, or the view you are in"
            [ "where"; "current"; "directory"; "path"; "location"; "view" ]
            []
        |> CommandSpec.readOnly
      Run =
        fun invocation ->
            async {
                return
                    Invocation.pure' (
                        match invocation.Location.View with
                        | Some expr -> Value.Query expr
                        | Option.None -> Value.Text invocation.Location.Folder)
            } }

// ------------------------------------------------------------------------ mkdir

let mkdir (newId: IdSource) (now: unit -> DateTimeOffset) =
    { Spec =
        CommandSpec.create
            "mkdir"
            "Create a directory"
            [ "create"; "make"; "directory"; "folder" ]
            [ Parameter.create "FolderName" "The name of the directory to create" |> Parameter.takes Takes.NewName ]
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

/// <summary>Writes text to a path: a new file, or new content for an existing one.</summary>
/// <remarks>
/// The whole of `write`, shared with `to-xml` and `to-csv` so that the three emit the
/// same events and undo, redo and history treat a document like any other file. `kind`
/// is what a file this creates is given, when the caller knows better than the
/// extension; a file that already exists keeps the kind it has.
/// </remarks>
let writeContent
    (newId: IdSource)
    (now: unit -> DateTimeOffset)
    (invocation: Invocation)
    (written: string)
    (kind: string option)
    (text: string)
    : Async<Outcome<CommandResult>> =
    async {
        let absolute = Files.normalise invocation.Location.Folder written
        let folder, name = Files.split absolute

        if not (Files.folderExists invocation.Projection folder) then
            return Error(Fault.directoryDoesNotExist folder)
        else
            // The content goes to the blob store first and the event carries only
            // hashes, so keeping the previous version costs nothing.
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
                      Attributes = baseAttributes name (defaultArg kind (Files.inferKind name)) folder (now ())
                      Content = None }

                return
                    Invocation.withEvents
                        (Record.toValue { record with Content = Some hash })
                        [ FileCreated record; ContentChanged(record.Id, None, Some hash) ]
    }

let write (newId: IdSource) (now: unit -> DateTimeOffset) =
    { Spec =
        CommandSpec.create
            "write"
            "Write text to a file, replacing its contents"
            [ "write"; "save"; "file"; "create"; "text" ]
            [ Parameter.create "path" "The file to write" |> Parameter.takes Takes.Path
              Parameter.create "text" "What to write" |> Parameter.piped |> Parameter.takes Takes.Value ]
      Run =
        fun invocation ->
            let text = Invocation.value "text" invocation |> Value.display
            writeContent newId now invocation (Invocation.text "path" invocation) None text }

// -------------------------------------------------------------------------- cat

/// <summary>A file's text, with the absolute path it was read from.</summary>
/// <remarks>
/// Shared with `from-xml` and `from-csv`, which need the path for their messages and
/// the same answers to a folder or a missing file that `cat` gives.
/// </remarks>
let readContent (invocation: Invocation) (written: string) : Async<Outcome<string * string>> =
    async {
        let absolute = Files.normalise invocation.Location.Folder written

        match Files.resolve invocation.Projection invocation.Location written with
        | Error fault -> return Error fault
        | Ok record when Record.isFolder record -> return Error(Fault.isADirectory absolute)
        | Ok record ->
            match record.Content with
            // A file that has never been written has no blob. That is empty text, not a
            // missing file: `save` makes records with attributes and no content at all.
            | None -> return Ok(absolute, "")
            | Some hash ->
                let! content = invocation.Blobs.Get hash
                return Ok(absolute, defaultArg content "")
    }

let cat =
    { Spec =
        CommandSpec.create
            "cat"
            "Read a file and return its text"
            [ "read"; "print"; "show"; "file"; "contents"; "type" ]
            [ Parameter.create "path" "The file to read" |> Parameter.piped |> Parameter.takes Takes.Path ]
        |> CommandSpec.readOnly
      Run =
        fun invocation ->
            async {
                let! content = readContent invocation (Invocation.text "path" invocation)
                return content |> Outcome.map (fun (_, text) -> { Value = Value.Text text; Events = [] })
            } }

// --------------------------------------------------------------------------- rm

let rm =
    { Spec =
        CommandSpec.create
            "rm"
            "Delete a file or an empty directory"
            [ "remove"; "delete"; "erase"; "file"; "directory" ]
            [ Parameter.create "path" "What to delete" |> Parameter.piped |> Parameter.takes Takes.Path ]
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
            [ Parameter.create "sourcePathAndFile" "The file to copy" |> Parameter.takes Takes.Path
              Parameter.create "targetPath" "The directory to copy it into" |> Parameter.takes Takes.Place ]
      Run =
        fun invocation ->
            async {
                let sourceWritten = Invocation.text "sourcePathAndFile" invocation
                let targetWritten = Invocation.text "targetPath" invocation

                match Files.resolve invocation.Projection invocation.Location sourceWritten with
                | Error fault -> return Error fault
                // Copying the folder's record alone made an empty folder that read as a
                // copy. A recursive copy is a tree of records in one line, which is what
                // `rm` declines to delete for the same reason.
                | Ok source when Record.isFolder source ->
                    return Error(Fault.cannotCopyADirectory (Files.pathOf invocation.Projection source))
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

/// <summary>Why an attribute cannot be written by hand, if it cannot.</summary>
/// <remarks>
/// Shared by `attr` and `save`, the two ways a user names attributes, so a tag cannot
/// put a record somewhere `attr` would have refused to move it. `size` has a message of
/// its own because it is not the terminal's to set either: it is not stored at all.
/// </remarks>
let private unwritable (name: string) =
    if name = Attributes.size then
        Some(Fault.computedFromContent name)
    elif List.contains name Attributes.reserved && not (List.contains name settableReserved) then
        Some(Fault.setByTheTerminal name)
    else
        None

/// <summary>Whether a change of kind leaves decision 0016 true.</summary>
/// <remarks>
/// Conservative on purpose: a folder stays a folder, empty or not, and a file with
/// content never becomes one. A folder that became a note would strand whatever names
/// it in `folder`, and a note with content that became a folder would be a folder with
/// content, which 0016 says there is not. A record with no content at all — a saved tag
/// — may become a folder, because a record with no content is all a folder is.
/// </remarks>
let private kindFault (record: FileRecord) (path: string) (kind: string) =
    if Record.isFolder record && kind <> Value.folderKind then
        Some(Fault.directoryStaysADirectory path)
    elif not (Record.isFolder record) && kind = Value.folderKind && record.Content.IsSome then
        Some(Fault.contentCannotBeADirectory path)
    else
        None

/// <summary>What renaming a folder carries with it.</summary>
/// <remarks>
/// A folder's path is its parent's path plus its name (decision 0016), and every record
/// under it holds that path in its `folder` attribute, so the new name has to be written
/// into each of them — and into the session's location, when it is standing inside —
/// or they are left naming a folder that no longer exists. They are events of the same
/// line, so one `undo` puts everything back (decision 0015). A child's `modified` is
/// left alone: nothing about the child changed, only the name of where it is.
/// </remarks>
let private carried (projection: Projection) (location: Location) (oldPath: string) (newPath: string) =
    let moved (folder: string) = newPath + folder.Substring oldPath.Length

    let descendants =
        projection.Files
        |> Map.toList
        |> List.map snd
        |> List.filter (fun record -> Files.isAncestorOf oldPath (Record.folder record))
        // Shallowest first, so the store sees each record land in a folder that has
        // already moved.
        |> List.sortBy (fun record -> (Record.folder record).Length, Record.name record)
        |> List.map (fun record ->
            AttributesChanged(
                record.Id,
                record.Attributes,
                Map.add Attributes.folder (Value.Text(moved (Record.folder record))) record.Attributes
            ))

    let following =
        if Files.isAncestorOf oldPath location.Folder then
            [ LocationChanged(location, { location with Folder = moved location.Folder }) ]
        else
            []

    descendants @ following

let attr (now: unit -> DateTimeOffset) =
    { Spec =
        CommandSpec.create
            "attr"
            "Show a file's attributes, or set them with name=value"
            [ "attribute"; "tag"; "metadata"; "property"; "label" ]
            [ Parameter.create "path" "The file" |> Parameter.piped |> Parameter.takes Takes.Path
              Parameter.assignments "assignments" "Attributes to set, written name=value" ]
      Run =
        fun invocation ->
            async {
                match Files.resolve invocation.Projection invocation.Location (Invocation.text "path" invocation) with
                | Error fault -> return Error fault
                | Ok record ->
                    if List.isEmpty invocation.Assignments then
                        // Reading: a table of name and value, so `attr x | where ...`
                        // is a question like any other.
                        let rows =
                            record.Attributes
                            |> Map.toList
                            |> List.map (fun (name, value) -> [ Value.Text name; value ])

                        return Invocation.pure' (Value.Table(Table.ofColumns [ "name"; "value" ] rows))
                    else
                        match invocation.Assignments |> List.tryPick (fst >> unwritable) with
                        | Some fault -> return Error fault
                        | None ->
                            let after =
                                invocation.Assignments
                                |> List.fold (fun map (name, value) -> Map.add name value map) record.Attributes
                                |> touch (now ())

                            let path = Files.pathOf invocation.Projection record
                            let folder = Record.folder record
                            let name = Attributes.text after Attributes.name
                            let renamed = name <> Record.name record

                            // Checked here as well as in the store, so a refusal is a
                            // stage failing, which `else` can recover from, rather than
                            // the commit failing once the line has finished.
                            let clash () =
                                Files.tryFindIn invocation.Projection folder name
                                |> Option.map (fun _ -> Fault.nameAlreadyExists (Value.joinPath folder name))

                            let problem =
                                (if renamed then Files.nameFault name |> Option.orElseWith clash else None)
                                |> Option.orElseWith (fun () -> kindFault record path (Attributes.text after Attributes.kind))

                            match problem with
                            | Some fault -> return Error fault
                            | None ->
                                let following =
                                    if renamed && Record.isFolder record then
                                        carried invocation.Projection invocation.Location path (Value.joinPath folder name)
                                    else
                                        []

                                return
                                    Invocation.withEvents
                                        (Record.toValue { record with Attributes = after })
                                        (AttributesChanged(record.Id, record.Attributes, after) :: following)
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
            [ Parameter.create "tag" "The tag to save" |> Parameter.piped |> Parameter.takes Takes.Value ]
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

                        let refused =
                            Files.nameFault name
                            |> Option.orElseWith (fun () ->
                                tag.Attributes |> Map.toList |> List.tryPick (fst >> unwritable))

                        if name = "" then
                            return Error(Fault.create Invalid "A saved tag needs a 'name' attribute.")
                        elif refused.IsSome then
                            return Error refused.Value
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

// -------------------------------------------------------------------- save-view

/// <summary>Keeps a predicate as a file, so a question becomes a place.</summary>
/// <remarks>
/// Decision 0013's last step: a view is an ordinary record of kind `view` whose content
/// is the predicate text. It therefore appears in `ls`, can be tapped, renamed, undone
/// and deleted like anything else, and `cd` on it enters the query it holds. Nothing in
/// the store knows about views except this command and `cd`.
/// </remarks>
let saveView (newId: IdSource) (now: unit -> DateTimeOffset) =
    { Spec =
        CommandSpec.create
            "save-view"
            "Save a predicate as a view you can enter with cd"
            [ "view"; "save"; "query"; "bookmark"; "keep" ]
            [ Parameter.create "name" "What to call the view" |> Parameter.takes Takes.NewName
              Parameter.predicate "predicate" "The predicate the view stands for" ]
      Run =
        fun invocation ->
            async {
                let name = Invocation.text "name" invocation

                match Invocation.predicate "predicate" invocation with
                | Option.None -> return Error(Fault.needsAPredicate "save-view")
                | Some expr when not (Expr.isPredicate expr) -> return Error(Fault.needsAPredicate "save-view")
                | Some expr ->
                    let folder = invocation.Location.Folder

                    if name = "" then
                        return Error(Fault.create Invalid "A view needs a name.")
                    elif (Files.nameFault name).IsSome then
                        return Error (Files.nameFault name).Value
                    elif (Files.tryFindIn invocation.Projection folder name).IsSome then
                        return Error(Fault.targetFileExists (Value.joinPath folder name))
                    else
                        // The predicate as it reads, not as it parsed: a view is a file
                        // someone can `cat`, and the text has to be something they could
                        // have typed.
                        let! hash = invocation.Blobs.Put(Expr.display expr)

                        let record =
                            { Id = newId ()
                              Attributes = baseAttributes name Value.viewKind folder (now ())
                              Content = None }

                        return
                            Invocation.withEvents
                                (Record.toValue { record with Content = Some hash })
                                [ FileCreated record; ContentChanged(record.Id, None, Some hash) ]
            } }
