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
    /// read in the terminal, with `cat`, rather than printed at the top of every visit.
    /// </remarks>
    let guideFiles = embedded "guide" ".txt"

    /// What `readme.txt` says: where to start.
    let readme =
        "This is a command line that runs in this browser tab.\n\n"
        + "The guide folder explains how it works, one idea per file. Start with the first:\n\n"
        + "  cat guide/1-start.txt\n\n"
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
            Content = Some "Try: ls, cd documents, mkdir scratch, echo \"hello\"" } ]
        @ exampleFiles
        @ guideFiles

    let standard newId now : Seed = ofFiles newId now standardFiles
