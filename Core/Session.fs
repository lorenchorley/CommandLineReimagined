/// One terminal.
///
/// The whole of what a host needs: give it a log, initialise it, and execute lines.
/// Everything web-specific lives above this, and everything about the language lives
/// below it.
namespace CommandLineReimagined.Core

open System
open System.Collections.Generic
open System.Net.Http
open System.Threading
open CommandLineReimagined.Parsing

/// What executing a line produced.
type Response =
    { Source: string
      /// What the command wrote while it ran, such as a progress bar.
      Output: string list
      Result: Value option
      Fault: Fault option
      Location: Location }

/// <summary>Options a host can vary.</summary>
/// <remarks>
/// A record rather than a dozen optional constructor arguments, because the desktop
/// shell and the browser differ in exactly these four things and agree on everything
/// else.
/// </remarks>
type SessionOptions =
    { Clock: unit -> DateTimeOffset
      NewId: Commands.Files.IdSource
      HttpClient: unit -> HttpClient
      /// What `exit` means. The browser has nothing to close and does nothing.
      Exit: unit -> unit }

[<RequireQualifiedAccess>]
module SessionOptions =

    let defaults =
        { Clock = fun () -> DateTimeOffset.UtcNow
          NewId = Commands.Files.guidSource
          HttpClient = fun () -> new HttpClient()
          Exit = ignore }

/// A written run of text that a command can keep changing, for a progress figure.
type CapturedText(initial: string, changed: unit -> unit) =
    let mutable text = initial

    member _.Text = text

    interface IOutputText with
        member _.Text
            with get () = text
            and set value =
                if text <> value then
                    text <- value
                    changed ()

/// One line of output, made of the runs written to it.
type CapturedLine(changed: unit -> unit) =
    let segments = ResizeArray<CapturedText>()

    member _.Text =
        segments |> Seq.map (fun (s: CapturedText) -> s.Text) |> String.concat ""

    interface IOutputLine with
        member _.Write text =
            let segment = CapturedText(text, changed)
            segments.Add segment
            changed ()
            segment :> IOutputText

/// <summary>Collects what a command writes while it runs, and says so as it changes.</summary>
/// <remarks>
/// Each line keeps its written runs separately, so a command that writes a label and
/// then keeps updating a figure after it changes only the figure. Empty lines are
/// dropped from what is reported, which is how a command abandons a line it turned out
/// to have nothing to say on.
/// </remarks>
type CapturingOutput(changed: string list -> unit) =
    let lines = ResizeArray<CapturedLine>()

    member _.Lines =
        lines
        |> Seq.map (fun (line: CapturedLine) -> line.Text)
        |> Seq.filter (fun text -> text.Length > 0)
        |> List.ofSeq

    member this.Notify() = changed this.Lines

    interface IOutput with
        member this.NewLine() =
            let line = CapturedLine(this.Notify)
            lines.Add line
            line :> IOutputLine

type Session(log: ILog, options: SessionOptions, seed: Seed) =

    let store = Store(log, options.Clock)
    let parser = CommandLineParser()

    let blobs =
        { new IBlobs with
            member _.Put content = store.PutBlob content
            member _.Get hash = store.GetBlob hash }

    let storeAccess =
        { Undo = store.Undo
          Redo = store.Redo
          History = store.History
          Exit = options.Exit }

    let commands =
        [ Commands.Files.ls
          Commands.Files.cd
          Commands.Files.up
          Commands.Files.pwd
          Commands.Files.mkdir options.NewId options.Clock
          Commands.Files.cp options.NewId options.Clock
          Commands.Files.cat
          Commands.Files.write options.NewId options.Clock
          Commands.Files.rm
          Commands.Files.attr options.Clock
          Commands.Files.save options.NewId options.Clock
          Commands.Values.echo
          Commands.Values.set
          Commands.Values.vars
          Commands.Async.progress
          Commands.Async.download options.HttpClient options.NewId options.Clock
          Commands.Meta.undo storeAccess
          Commands.Meta.redo storeAccess
          Commands.Meta.history storeAccess
          Commands.Meta.exit storeAccess
          Commands.Meta.unknown ]

    let evaluator = Evaluator(commands, store, blobs)
    let outputChanged = Event<int * string list>()

    let mutable running: CancellationTokenSource option = None
    let mutable initialised = false

    new(log: ILog) = Session(log, SessionOptions.defaults, Seed.none)
    new(log: ILog, seed: Seed) = Session(log, SessionOptions.defaults, seed)

    /// <summary>Replays the log, and seeds it if it was empty.</summary>
    /// <remarks>
    /// Seeding is a transaction like any other, so it appears in `history` and could
    /// in principle be undone. It runs only on an empty log, so a browser reload
    /// restores what was there rather than adding the seed again.
    /// </remarks>
    member _.Initialize() =
        async {
            do! store.Initialize()

            if List.isEmpty store.Transactions then
                let! events = seed blobs

                if not (List.isEmpty events) then
                    let! _ = store.Commit "seed" events
                    ()

            initialised <- true
        }

    member _.IsInitialised = initialised

    member _.Commands = evaluator.Specs

    member _.Location = store.Current.Location

    member _.Projection = store.Current

    member _.Store = store

    /// Whether a command is currently running.
    member _.IsRunning =
        match running with
        | Some source -> not source.IsCancellationRequested
        | None -> false

    /// Raised while a command runs, carrying the execution id and the complete current
    /// lines, so a listener can redraw without tracking deltas.
    member _.OutputChanged = outputChanged.Publish

    member _.StoreChanged = store.Changed

    /// <summary>Parses and runs a line. Never raises.</summary>
    /// <remarks>
    /// Every way this can go wrong is a `Fault` in the response: a parse failure is
    /// `Syntax`, a cancelled command is `Cancelled`, and a defect is `Internal`. The
    /// session boundary is where decision 0006's promise is kept.
    /// </remarks>
    member this.Execute(source: string, executionId: int, cancel: CancellationToken) =
        async {
            let source = if isNull source then "" else source
            let output = CapturingOutput(fun lines -> outputChanged.Trigger(executionId, lines))

            let respond fault result =
                { Source = source
                  Output = output.Lines
                  Result = result
                  Fault = fault
                  Location = store.Current.Location }

            if not initialised then
                return respond (Some(Fault.notInitialised ())) None
            elif this.IsRunning then
                return respond (Some(Fault.alreadyRunning ())) None
            else
                let parsed = parser.Parse<Tree.Node> source

                let tree =
                    parsed.Match((fun tree -> Ok tree), (fun _ -> Error(Fault.couldNotParse ())))

                match tree with
                | Error fault -> return respond (Some fault) None
                | Ok tree ->
                    let source' = CancellationTokenSource.CreateLinkedTokenSource cancel
                    running <- Some source'

                    try
                        try
                            let! result = evaluator.Execute tree source output source'.Token

                            match result with
                            | Error fault -> return respond (Some fault) None
                            | Ok execution -> return respond None (Some execution.Value)
                        with
                        | :? OperationCanceledException -> return respond (Some(Fault.cancelled ())) None
                        | exn -> return respond (Some(Fault.internalError exn)) None
                    finally
                        running <- None
                        source'.Dispose()
        }

    member this.Execute(source: string) =
        this.Execute(source, 0, CancellationToken.None)

    /// Stops the command in flight, if there is one.
    member _.Cancel() =
        match running with
        | Some source when not source.IsCancellationRequested ->
            source.Cancel()
            true
        | _ -> false

    member this.Complete(text: string) =
        Completion.suggest evaluator.Specs store.Current text

    /// The variables in scope, ordered by name.
    member _.Variables() = store.Current.Variables |> Map.toList

    member _.History() = store.History()
