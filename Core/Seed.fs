/// What a brand new session starts with.
///
/// An empty filesystem makes `ls` look broken rather than empty, so a fresh log gets
/// things to look at: a readme, the guide it points to, and the example programs. It is seeded once, as an ordinary transaction, so it shows
/// up in `history` and a reload replays it rather than adding it again.
namespace CommandLineReimagined.Core

open System

/// <summary>A file to create when the log is empty.</summary>
/// <remarks>
/// Described rather than given as events, because content has to reach the blob store
/// before an event can name its hash, and the blob store belongs to the log the
/// session is handed. A host says what it wants; the session works out the events.
/// </remarks>
type SeedFile =
    { Name: string
      Folder: string
      /// `None` makes a folder; `Some text` makes a file with that content.
      Content: string option }

/// Turning a description into events needs somewhere to put content, so a seed is a
/// function of the blob store rather than a list.
type Seed = IBlobs -> Async<Event list>

[<RequireQualifiedAccess>]
module Seed =

    let none: Seed = fun _ -> async.Return []

    let ofFiles (newId: unit -> FileId) (now: unit -> DateTimeOffset) (files: SeedFile list) : Seed =
        fun blobs ->
            async {
                let mutable events = []

                for file in files do
                    let id = newId ()
                    let at = Value.Text((now ()).ToString "o")

                    let kind =
                        match file.Content with
                        | None -> Value.folderKind
                        | Some _ -> Files.inferKind file.Name

                    let record =
                        { Id = id
                          Attributes =
                            Map.ofList
                                [ Attributes.name, Value.Text file.Name
                                  Attributes.kind, Value.Text kind
                                  Attributes.folder, Value.Text file.Folder
                                  Attributes.created, at
                                  Attributes.modified, at ]
                          Content = None }

                    events <- events @ [ FileCreated record ]

                    match file.Content with
                    | None -> ()
                    | Some content ->
                        let! hash = blobs.Put content
                        events <- events @ [ ContentChanged(id, None, Some hash) ]

                return events
            }

    /// <summary>The example programs, read out of the assembly they travel in.</summary>
    /// <remarks>
    /// Embedded rather than copied beside the binary, because the browser build is a
    /// set of assemblies downloaded into a tab and there is no "beside" to copy to.
    /// </remarks>
    let private embedded (folder: string) (extension: string) =
        let assembly = Reflection.Assembly.GetExecutingAssembly()
        let prefix = folder + "/"

        assembly.GetManifestResourceNames()
        |> Array.filter (fun name -> name.StartsWith prefix && name.EndsWith extension)
        |> Array.sortWith (fun a b -> String.CompareOrdinal(a, b))
        |> Array.map (fun name ->
            use stream = assembly.GetManifestResourceStream name
            use reader = new IO.StreamReader(stream)

            { Name = name.Substring(prefix.Length)
              Folder = "/" + folder
              Content = Some(reader.ReadToEnd()) })
        |> List.ofArray

    let exampleFiles = embedded "examples" ".clr"

    /// <summary>The guide: one file per idea, numbered in the order to read them.</summary>
    /// <remarks>
    /// The page's banner only points here, so what the terminal is and how to use it is
    /// read in the terminal, with `read`, rather than printed at the top of every visit.
    /// </remarks>
    let guideFiles = embedded "guide" ".txt"

    /// What `readme.txt` says: where to start.
    let readme =
        "This is a command line that runs in this browser tab.\n\n"
        + "The guide folder explains how it works, one idea per file. Start with the first:\n\n"
        + "  read guide/1-start.txt\n\n"
        + "or list them all:\n\n"
        + "  ls guide\n"

    /// The filesystem the terminal has always started with, as records.
    let standardFiles =
        [ { Name = "documents"; Folder = "/"; Content = None }
          { Name = "examples"; Folder = "/"; Content = None }
          { Name = "guide"; Folder = "/"; Content = None }
          { Name = "projects"; Folder = "/"; Content = None }
          { Name = "readme.txt"; Folder = "/"; Content = Some readme }
          { Name = "notes.txt"
            Folder = "/documents"
            Content = Some "Try: ls, in documents, mkdir scratch, echo \"hello\"" } ]
        @ exampleFiles
        @ guideFiles

    let standard newId now : Seed = ofFiles newId now standardFiles

    /// What `readme.txt` said before the guide existed, so a log begun then can be told
    /// apart from one whose readme someone has rewritten.
    let private readmeBeforeTheGuide = "This filesystem lives in the browser tab."

    /// Whether a log has ever had a `/guide`, even one since deleted.
    let private everHadTheGuide (history: Transaction list) =
        history
        |> List.exists (fun transaction ->
            transaction.Events
            |> List.exists (function
                | FileCreated record -> Record.name record = "guide" && Record.folder record = Files.root
                | _ -> false))

    /// <summary>What a log begun before the guide needs to have it (decision 0036).</summary>
    /// <remarks>
    /// The seed runs only on an empty log, so a visitor who came before the guide existed
    /// would never get it without `reset`, which would cost them their files. This adds
    /// it once, and never to a log that has ever created a `/guide`, so one deleted on
    /// purpose stays deleted. `readme.txt` is brought up to date only if it still says
    /// exactly what the old seed wrote; a readme someone has changed is theirs.
    /// </remarks>
    let guideFor newId now (history: Transaction list) (projection: Projection) : Seed =
        fun blobs ->
            async {
                if everHadTheGuide history then
                    return []
                else
                    let! guide = ofFiles newId now ({ Name = "guide"; Folder = "/"; Content = None } :: guideFiles) blobs

                    let! readmeEvents =
                        match Files.tryFindIn projection Files.root "readme.txt" with
                        | Some record ->
                            async {
                                let! old =
                                    match record.Content with
                                    | Some hash -> blobs.Get hash
                                    | None -> async.Return None

                                if old = Some readmeBeforeTheGuide then
                                    let! hash = blobs.Put readme
                                    return [ ContentChanged(record.Id, record.Content, Some hash) ]
                                else
                                    return []
                            }
                        | None -> async.Return []

                    return guide @ readmeEvents
            }

    /// <summary>Every record a line someone typed has created, changed or deleted.</summary>
    /// <remarks>
    /// Undoing and redoing are lines too: they are undoable transactions, so a file
    /// written to and then put back by `undo` has still been touched.
    /// </remarks>
    let private touchedByAnyone (history: Transaction list) =
        history
        |> List.filter (fun transaction -> transaction.Undoable)
        |> List.collect (fun transaction -> transaction.Events)
        |> List.choose (function
            | FileCreated record
            | FileDeleted record -> Some record.Id
            | AttributesChanged(id, _, _)
            | ContentChanged(id, _, _) -> Some id
            | _ -> None)
        |> Set.ofList

    /// <summary>What brings the seeded files nobody has changed to the seed's current text (decision 0040).</summary>
    /// <remarks>
    /// A file the seed describes is still the terminal's when the record at its path has
    /// never been named by a line anyone typed. Every record is made by some transaction,
    /// so one no undoable transaction has named was made by a system one: the seed, or
    /// the guide added by <c>guideFor</c>. Its content is replaced when it differs from
    /// the seed's; its attributes, `modified` included, are left as they were, as 0036's
    /// readme update left them. A file someone has written to, renamed, moved, tagged or
    /// deleted is theirs: it has been named by their line, or is not at the path any
    /// more. Content is compared by hash, which is what the log addresses it by, so a
    /// load that has nothing to change puts nothing in the blob store.
    /// </remarks>
    let updatesFor (files: SeedFile list) (history: Transaction list) (projection: Projection) : Seed =
        fun blobs ->
            async {
                let theirs = touchedByAnyone history
                let mutable events = []

                for file in files do
                    match file.Content with
                    | None -> ()
                    | Some text ->
                        match Files.tryFindIn projection file.Folder file.Name with
                        | Some record when
                            not (Set.contains record.Id theirs)
                            && not (Record.isFolder record)
                            && record.Content <> Some(Hash.ofText text)
                            ->
                            let! hash = blobs.Put text
                            events <- events @ [ ContentChanged(record.Id, record.Content, Some hash) ]
                        | _ -> ()

                return events
            }
