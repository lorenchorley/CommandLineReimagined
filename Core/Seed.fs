/// What a brand new session starts with.
///
/// An empty filesystem makes `ls` look broken rather than empty, so a fresh log gets
/// three things to look at. It is seeded once, as an ordinary transaction, so it shows
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
    let exampleFiles =
        let assembly = Reflection.Assembly.GetExecutingAssembly()

        assembly.GetManifestResourceNames()
        |> Array.filter (fun name -> name.StartsWith "examples/" && name.EndsWith ".clr")
        |> Array.sortWith (fun a b -> String.CompareOrdinal(a, b))
        |> Array.map (fun name ->
            use stream = assembly.GetManifestResourceStream name
            use reader = new IO.StreamReader(stream)

            { Name = name.Substring("examples/".Length)
              Folder = "/examples"
              Content = Some(reader.ReadToEnd()) })
        |> List.ofArray

    /// The filesystem the terminal has always started with, as records.
    let standardFiles =
        [ { Name = "documents"; Folder = "/"; Content = None }
          { Name = "examples"; Folder = "/"; Content = None }
          { Name = "projects"; Folder = "/"; Content = None }
          { Name = "readme.txt"
            Folder = "/"
            Content = Some "This filesystem lives in the browser tab." }
          { Name = "notes.txt"
            Folder = "/documents"
            Content = Some "Try: ls, cd documents, mkdir scratch, echo \"hello\"" } ]
        @ exampleFiles

    let standard newId now : Seed = ofFiles newId now standardFiles
