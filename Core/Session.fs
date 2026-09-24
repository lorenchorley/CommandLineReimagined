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

/// <summary>What a line did to the log, as a scrollback needs to know it.</summary>
/// <remarks>
/// A host that draws one entry per line can use this to make `undo` look like what
/// the person meant by it: the line it reverses goes away, and `redo` brings it back,
/// rather than each leaving a line of its own that describes the log underneath.
/// Every number is the sequence number of a line someone ran, never of the
/// compensation that reversed it, so the host only has to remember one number per line.
/// </remarks>
type LogChanges =
    { /// The lines this one committed. Usually one, none for a line that only read,
      /// and one per line of the script for `run` (decision 0020).
      Committed: int64 list
      /// The lines this one undid.
      Undone: int64 list
      /// The lines this one redid: the line the undo had reversed, not the undo.
      Redone: int64 list
      /// Whether `reset` emptied the log while this ran. Sequence numbers start again
      /// after it, so every number remembered from before now names nothing.
      Reset: bool }

[<RequireQualifiedAccess>]
module LogChanges =

    let none =
        { Committed = []
          Undone = []
          Redone = []
          Reset = false }

/// What executing a line produced.
type Response =
    { Source: string
      /// What the command wrote while it ran, such as a progress bar.
      Output: string list
      Result: Value option
      Fault: Fault option
      Location: Location
      Changes: LogChanges
      /// <summary>How to call the command, when the line called it wrongly (decision 0038).</summary>
      /// <remarks>The value `help &lt;command&gt;` answers. `None` on every other line.</remarks>
      Guide: Value option
      /// <summary>What the terminal says of its own about the line (decisions 0041 to 0044).</summary>
      /// <remarks>
      /// A failed line's are its fault's; a line that succeeded has those its stages
      /// gave. Every fix is resolved to a whole `Fix.Line` of `Source`. Guidance, drawn
      /// apart from output, and never part of `Result`.
      /// </remarks>
      Notes: Note list }

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

    /// <summary>Builds options from plain .NET delegates.</summary>
    /// <remarks>
    /// So that a C# host never has to construct an F# function. `FuncConvert` is the
    /// obvious way to do it from the other side and is the wrong one twice over: it
    /// puts F# types in the adapter, which the architecture says stop at the boundary,
    /// and the WebAssembly trimmer removes its generic overloads, so a host that used
    /// it compiled and then failed at runtime with a missing method.
    /// </remarks>
    let ofDelegates (clock: Func<DateTimeOffset>) (newId: Func<string>) (client: Func<HttpClient>) (exit: Action) =
        { Clock = clock.Invoke
          NewId = newId.Invoke
          HttpClient = client.Invoke
          Exit = exit.Invoke }

    /// The filesystem a fresh session starts with, built from these options, so a host
    /// does not have to take them apart to pass the two functions back in.
    let standardSeed (options: SessionOptions) : Seed =
        Seed.ofFiles options.NewId options.Clock Seed.standardFiles

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

    let blobsForSeed = blobs

    /// Appends the seed, and answers how many records it created. Used both on a fresh
    /// log and after `reset`, so the two cannot describe different starting states.
    let applySeed () =
        async {
            let! events = seed blobsForSeed

            if List.isEmpty events then
                return 0
            else
                // Recorded and replayed like any line, and marked as nobody's to
                // undo (decision 0018).
                let! _ = store.CommitSystem "seed" events

                return
                    events
                    |> List.sumBy (function
                        | FileCreated _ -> 1
                        | _ -> 0)
        }

    /// <summary>Running one line, and the whole command list, before either exists.</summary>
    /// <remarks>
    /// `run` needs the evaluator and `help` needs the command list, and both are
    /// commands, so both are in the list the evaluator is built from. These are filled
    /// in immediately after it is, which is the smallest knot that ties: a command
    /// holds a function, not a session.
    /// </remarks>
    let mutable runLine: string -> IOutput -> CancellationToken -> Async<Outcome<Value * Note list>> =
        fun _ _ _ -> async.Return(Error(Fault.notInitialised ()))

    let mutable specs: CommandSpec list = []

    let storeAccess =
        { Undo = store.Undo
          Redo = store.Redo
          History = store.History
          Exit = options.Exit
          RunLine = fun source output cancel -> runLine source output cancel
          Reset =
            fun () ->
                async {
                    do! store.Reset()
                    return! applySeed ()
                } }

    /// <summary>The command that reports an unknown name, with keywords among the nearest.</summary>
    /// <remarks>
    /// The evaluator hands it the names a slip away, and only names; an old name such as
    /// `cd` is a keyword of `in` (decision 0037), not a slip of it, so the nearest are
    /// worked out again here, where the specs and their keywords are known. The same
    /// function answers `help cd`.
    /// </remarks>
    let unknownByKeyword =
        { Commands.Meta.unknown with
            Run =
                fun invocation ->
                    async {
                        let name = Invocation.text "name" invocation
                        return Error(Fault.unknownCommand name (Nearest.commands specs name))
                    } }

    let commands =
        [ Commands.Files.ls
          Commands.Files.into
          Commands.Files.out
          Commands.Files.back
          Commands.Files.pwd
          Commands.Files.find
          Commands.Files.saveView options.NewId options.Clock
          Commands.Files.mkdir options.NewId options.Clock
          Commands.Files.cp options.NewId options.Clock
          Commands.Files.read
          Commands.Files.write options.NewId options.Clock
          Commands.Files.rm
          Commands.Files.attr options.Clock
          Commands.Files.save options.NewId options.Clock
          Commands.Values.echo
          Commands.Values.set
          Commands.Values.vars
          Commands.Values.isFault
          Commands.Async.progress
          Commands.Async.download options.HttpClient options.NewId options.Clock
          Commands.Tables.where
          Commands.Tables.select
          Commands.Tables.sort
          Commands.Tables.take
          Commands.Tables.skip
          Commands.Tables.first
          Commands.Tables.last
          Commands.Tables.count
          Commands.Tables.distinct
          Commands.Tables.group
          Commands.Tables.columns
          Commands.Tables.rows
          Commands.Tables.table
          Commands.Documents.fromXml
          Commands.Documents.toXml options.NewId options.Clock
          Commands.Documents.pick
          Commands.Documents.fromCsv
          Commands.Documents.toCsv options.NewId options.Clock
          Commands.Meta.undo storeAccess
          Commands.Meta.redo storeAccess
          Commands.Meta.history storeAccess
          Commands.Meta.reset storeAccess
          Commands.Meta.run storeAccess
          Commands.Meta.helpWith (fun _ word -> Nearest.commands specs word) (fun () -> specs)
          Commands.Meta.exit storeAccess
          unknownByKeyword ]

    let evaluator = Evaluator(commands, store, blobs)
    let outputChanged = Event<int * string list>()

    /// The two knots tied. A line that `run` executes goes through the evaluator like
    /// any other, so it commits its own transaction (decision 0020) and a fault in it
    /// is an ordinary fault with the script's name added.
    do
        specs <- evaluator.Specs

        runLine <-
            fun source output cancel ->
                async {
                    let parsed = parser.Parse<Tree.Node> source

                    match parsed.Match((fun tree -> Ok tree), (fun error -> Error(Fault.ofParseError error))) with
                    | Error fault -> return Error fault
                    | Ok tree ->
                        let! result = evaluator.Execute tree source output cancel
                        return result |> Outcome.map (fun execution -> execution.Value, execution.Notes)
                }

    /// <summary>What a line's fault says, notes and all, for its response.</summary>
    /// <remarks>
    /// A missing file, folder or variable is found by a command that cannot see what
    /// else there is, so the nearest ones are named here (decision 0042), from what
    /// there is now: after a failed line, what there was before it. Every fix is then
    /// made a whole line of `source` (0044, `Note.resolve`).
    /// </remarks>
    let faultNotes (source: string) (fault: Fault) : Note list =
        let projection = store.Current

        let nearest =
            match Fault.missing fault with
            | Some(Missing.File path) -> Nearest.pathNote projection projection.Location source path false
            | Some(Missing.Folder path) -> Nearest.pathNote projection projection.Location source path true
            | Some(Missing.Variable name) ->
                Nearest.variableNote (projection.Variables |> Map.toList |> List.map fst) source name
            | None -> []

        fault.Notes @ nearest |> List.map (Note.resolve source)

    /// <summary>The command a line called wrongly, when that is why it failed (decision 0038).</summary>
    /// <remarks>
    /// A binding fault, stamped with the stage it failed in, where that stage names a
    /// command that exists. With `else` the fault is the last branch's, so the stage is
    /// counted in the last pipeline. A binding fault stamped on a stage can have come
    /// from inside it rather than from its call: from a pipeline in parentheses, a line
    /// of the script `run` ran, or a `??` default. Every fault about a call names its
    /// command first (`'sort' needs an argument for 'column'.`), so the fault must name
    /// this stage's command too. A fault raised while a command runs over what it was
    /// given (a file that is not there) is not a binding fault, and an unknown name
    /// has no command.
    /// </remarks>
    let calledWrongly (tree: Tree.Node) (fault: Fault) : CommandSpec option =
        let pipeline =
            match tree with
            | :? Tree.Pipeline as pipeline -> Some pipeline
            | :? Tree.RecoveryLine as line when line.Pipelines.Count > 0 -> Some(Seq.last line.Pipelines)
            | _ -> None

        match fault.Kind, fault.Stage, pipeline with
        | Binding, Some stage, Some pipeline when stage >= 1 && stage <= pipeline.OrderedCommands.Count ->
            let expression = pipeline.OrderedCommands[stage - 1]

            let name =
                expression.Expression.Match(
                    (fun (f: Tree.Function) -> Some f.Id.Name),
                    (fun (c: Tree.Cli) -> Some c.Name.Name),
                    (fun (_: Tree.InstanceTag) -> None),
                    (fun (_: Tree.NestedPipeline) -> None),
                    (fun (_: Tree.VariableReference) -> None))

            name
            |> Option.bind (fun name ->
                evaluator.Specs
                |> List.tryFind (fun spec -> String.Equals(spec.Name, name, StringComparison.OrdinalIgnoreCase)))
            |> Option.filter (fun spec -> fault.Message.StartsWith(sprintf "'%s'" spec.Name, StringComparison.Ordinal))
            |> Option.orElse (
                name
                |> Option.bind (fun name ->
                    evaluator.Specs
                    |> List.tryFind (fun spec -> String.Equals(spec.Name, name, StringComparison.OrdinalIgnoreCase)))
                |> Option.filter (fun spec ->
                    // A question that never reads `$row` is a predicate argument written
                    // wrongly, and its own sentence names the fix; the help says the rest.
                    fault.Message.Contains " never reads $row"
                    && spec.Parameters |> List.exists (fun p -> p.Kind = ParamKind.Predicate)))
        // A value the command refused as the wrong kind for one of its parameters, such
        // as `ls | take x`: its message names the parameter (`'count' must be a whole
        // number`), which is what tells it from a failure met while the command ran.
        | Invalid, Some stage, Some pipeline when stage >= 1 && stage <= pipeline.OrderedCommands.Count ->
            let expression = pipeline.OrderedCommands[stage - 1]

            let name =
                expression.Expression.Match(
                    (fun (f: Tree.Function) -> Some f.Id.Name),
                    (fun (c: Tree.Cli) -> Some c.Name.Name),
                    (fun (_: Tree.InstanceTag) -> None),
                    (fun (_: Tree.NestedPipeline) -> None),
                    (fun (_: Tree.VariableReference) -> None))

            name
            |> Option.bind (fun name ->
                evaluator.Specs
                |> List.tryFind (fun spec -> String.Equals(spec.Name, name, StringComparison.OrdinalIgnoreCase)))
            |> Option.filter (fun spec ->
                spec.Parameters
                |> List.exists (fun p -> fault.Message.StartsWith(sprintf "'%s' " p.Name, StringComparison.OrdinalIgnoreCase)))
        | _ -> None

    /// <summary>What `help &lt;command&gt;` answers, as a line's guide (decision 0038).</summary>
    /// <remarks>
    /// Asked of `help` itself, the way a live view re-reads a line, so the guide cannot
    /// drift from what `help` says: nothing is committed and nothing reaches the
    /// screen. `help` writes the command's description through its output and answers
    /// the table of parameters; the guide is both, the description first, as one list,
    /// which a host flattens into a line of text and the table.
    /// </remarks>
    let guideFor (spec: CommandSpec) : Async<Value option> =
        async {
            let source = sprintf "help %s" spec.Name
            let parsed = parser.Parse<Tree.Node> source

            if not parsed.IsT0 then
                return None
            else
                let output = CapturingOutput ignore

                try
                    let! result = evaluator.Refresh parsed.AsT0 source output CancellationToken.None

                    match result with
                    | Ok execution ->
                        let description = output.Lines |> List.map Value.Text
                        return Some(Value.List(description @ [ execution.Value ]))
                    | Error _ -> return None
                with _ ->
                    return None
        }

    let mutable running: CancellationTokenSource option = None
    let mutable completing: CancellationTokenSource option = None

    /// What completion has learned about upstreams (decision 0031). A commit can change
    /// what any line answers, so every commit forgets it all.
    let shapes = ShapeCache()
    do store.Changed.Add(fun _ -> shapes.Clear())
    let mutable initialised = false
    let mutable replayed = 0

    /// <summary>Marks where the log is, and answers what has been appended since.</summary>
    /// <remarks>
    /// Read from the log rather than from what the commands returned, because `undo`
    /// and `redo` append straight away rather than returning events, and `run` commits
    /// once per line of its script. The log is the one place all of them agree.
    ///
    /// The seed is left out: it is not a line anyone ran (decision 0018), and `reset`
    /// is what appends it again.
    /// </remarks>
    let changesSince () =
        let epoch = store.Epoch

        let last =
            match store.Transactions with
            | [] -> 0L
            | transactions -> transactions |> List.map (fun t -> t.Seq) |> List.max

        fun () ->
            let reset = store.Epoch <> epoch
            let all = store.Transactions
            let find seq = all |> List.tryFind (fun t -> t.Seq = seq)

            let appended =
                all |> List.filter (fun t -> t.Undoable && (reset || t.Seq > last))

            let committed, undone, redone =
                appended
                |> List.fold
                    (fun (committed, undone, redone) t ->
                        match t.Compensates |> Option.map (fun target -> target, find target) with
                        | None -> t.Seq :: committed, undone, redone
                        // An undo compensates a line; a redo compensates that undo, and
                        // is reported as the line the undo had reversed.
                        | Some(target, Some reversed) when reversed.Compensates.IsNone ->
                            committed, target :: undone, redone
                        | Some(_, Some reversed) ->
                            committed, undone, reversed.Compensates.Value :: redone
                        | Some(_, None) -> committed, undone, redone)
                    ([], [], [])

            { Committed = List.rev committed
              Undone = List.rev undone
              Redone = List.rev redone
              Reset = reset }

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
            replayed <- List.length store.Transactions

            // Only on an empty log, so a reload replays what was there rather than
            // seeding a second copy over the top of it.
            if List.isEmpty store.Transactions then
                let! _ = applySeed ()
                ()

            initialised <- true
        }

    /// <summary>Brings a replayed log up to what the standard seed is today.</summary>
    /// <remarks>
    /// Two steps, each a transaction that is nobody's to undo, like the seed. First the
    /// guide, added once to a log that never had one (decision 0036). Then every seeded
    /// file nobody has changed is given the seed's current content (decision 0040), in
    /// one transaction with the source `seed update`, so a returning visitor reads the
    /// guide as it is now; a file someone has written to, renamed, moved, tagged or
    /// deleted is left as it is. Answers how many records it created. A host that seeds
    /// the standard filesystem calls it after `Initialize`; on a fresh log, or a second
    /// time, it finds nothing to do and commits nothing.
    /// </remarks>
    member _.BringUpToDate() =
        async {
            let! guide = Seed.guideFor options.NewId options.Clock store.Transactions store.Current blobsForSeed

            if not (List.isEmpty guide) then
                let! _ = store.CommitSystem "guide" guide
                ()

            let! updates = Seed.updatesFor Seed.standardFiles store.Transactions store.Current blobsForSeed

            if not (List.isEmpty updates) then
                let! _ = store.CommitSystem "seed update" updates
                ()

            return
                guide
                |> List.sumBy (function
                    | FileCreated _ -> 1
                    | _ -> 0)
        }

    /// How many transactions the log had when it was replayed. The page reports it, so
    /// that a restore that silently found nothing is visible rather than looking like
    /// a fresh session.
    member _.ReplayedCount = replayed

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
            let since = changesSince ()

            let respond fault result =
                { Source = source
                  Output = output.Lines
                  Result = result
                  Fault = fault
                  Location = store.Current.Location
                  Changes = since ()
                  Guide = None
                  Notes =
                    fault
                    |> Option.map (faultNotes source)
                    |> Option.defaultValue [] }

            if not initialised then
                return respond (Some(Fault.notInitialised ())) None
            elif this.IsRunning then
                return respond (Some(Fault.alreadyRunning ())) None
            else
                let parsed = parser.Parse<Tree.Node> source
                let tree = parsed.Match((fun tree -> Ok tree), (fun error -> Error(Fault.ofParseError error)))

                match tree with
                | Error fault -> return respond (Some fault) None
                | Ok tree ->
                    let source' = CancellationTokenSource.CreateLinkedTokenSource cancel
                    running <- Some source'

                    try
                        try
                            let! result = evaluator.Execute tree source output source'.Token

                            match result with
                            | Error fault ->
                                match calledWrongly tree fault with
                                | Some spec ->
                                    let! guide = guideFor spec
                                    return { respond (Some fault) None with Guide = guide }
                                | None -> return respond (Some fault) None
                            | Ok execution ->
                                return
                                    { respond None (Some execution.Value) with
                                        Notes = execution.Notes |> List.map (Note.resolve source) }
                        with
                        | :? OperationCanceledException -> return respond (Some(Fault.cancelled ())) None
                        | exn -> return respond (Some(Fault.internalError exn)) None
                    finally
                        running <- None
                        source'.Dispose()
        }

    member this.Execute(source: string) =
        this.Execute(source, 0, CancellationToken.None)

    /// <summary>Re-runs a line that only reads, leaving no trace (Phase 4).</summary>
    /// <remarks>
    /// A live view's whole mechanism. The page keeps the source of the listing it is
    /// showing and asks for it again whenever the store changes; this answers with a
    /// fresh result and appends nothing, so the scrollback, the history and `undo` are
    /// all exactly as they would have been. A line that would change something is
    /// refused, by name, before it runs.
    /// </remarks>
    member this.Refresh(source: string) = this.RefreshAt(source, None)

    /// <summary>Re-runs a line that only reads, from where it was first run (decision 0047).</summary>
    /// <remarks>
    /// `location` is the place the listing was answered in, which the host remembers
    /// from its first response. `None` is where the session is now, which is `Refresh`.
    /// </remarks>
    member this.RefreshAt(source: string, location: Location option) = this.RefreshWhere(source, Ok location)

    /// <summary>`RefreshAt` for a host that kept the place as text, as a response gives it.</summary>
    /// <remarks>
    /// `folder` is the folder and `view` the view's predicate as `Expr.display` wrote it,
    /// or empty for none. A view that no longer reads as a predicate is the refresh's
    /// fault, so a listing stays as it was rather than being asked somewhere else.
    /// </remarks>
    member this.RefreshFrom(source: string, folder: string, view: string) =
        let place =
            if String.IsNullOrWhiteSpace view then
                Ok(Some { Folder = folder; View = None })
            else
                Expr.parse "view" view |> Result.map (fun expr -> Some { Folder = folder; View = Some expr })

        this.RefreshWhere(source, place)

    member private _.RefreshWhere(source: string, place: Outcome<Location option>) =
        async {
            let source = if isNull source then "" else source
            let location = match place with Ok location -> location | Error _ -> None
            // A refresh is nobody's line, so its output goes nowhere: a progress bar
            // from a re-read would appear under an entry that nobody submitted.
            let output = CapturingOutput ignore

            let respond fault result =
                { Source = source
                  Output = output.Lines
                  Result = result
                  Fault = fault
                  Location = defaultArg location store.Current.Location
                  Changes = LogChanges.none
                  Guide = None
                  Notes =
                    fault
                    |> Option.map (faultNotes source)
                    |> Option.defaultValue [] }

            if not initialised then
                return respond (Some(Fault.notInitialised ())) None
            else
                let parsed = parser.Parse<Tree.Node> source

                match parsed.Match((fun tree -> Ok tree), (fun error -> Error(Fault.ofParseError error))), place with
                | Error fault, _
                | _, Error fault -> return respond (Some fault) None
                | Ok tree, Ok _ ->
                    try
                        let! result = evaluator.RefreshAt tree source output CancellationToken.None location

                        match result with
                        | Error fault -> return respond (Some fault) None
                        | Ok execution ->
                            return
                                { respond None (Some execution.Value) with
                                    Notes = execution.Notes |> List.map (Note.resolve source) }
                    with exn ->
                        return respond (Some(Fault.internalError exn)) None
        }

    /// Stops the command in flight, if there is one.
    member _.Cancel() =
        match running with
        | Some source when not source.IsCancellationRequested ->
            source.Cancel()
            true
        | _ -> false

    /// <summary>What a line could be previewed as, for completion (decision 0031).</summary>
    /// <remarks>
    /// The same path as `Refresh`: a line that would write is refused before it runs,
    /// and nothing is committed or shown. Any failure is `None`, because completion that
    /// cannot learn what flows in falls back rather than failing.
    /// </remarks>
    member private _.Preview (source: string) (cancel: CancellationToken) : Async<Value option> =
        async {
            let parsed = parser.Parse<Tree.Node> source

            if not initialised || not parsed.IsT0 then
                return None
            else
                try
                    let! result = evaluator.Refresh parsed.AsT0 source (CapturingOutput ignore) cancel

                    match result with
                    | Ok execution -> return Some execution.Value
                    | Error _ -> return None
                with _ ->
                    return None
        }

    /// Everything a provider is given for one line and cursor. A newer request cancels
    /// the one before it, so an upstream run for a stale keystroke stops.
    member private this.CompletionRequest(text: string, cursor: int) =
        let text = if isNull text then "" else text

        completing |> Option.iter (fun (previous: CancellationTokenSource) -> previous.Cancel())
        let current = new CancellationTokenSource()
        completing <- Some current

        Completion.request evaluator.Specs (this.Shapes current.Token) text cursor current.Token

    /// <summary>What `Shape` may use to learn what flows in, as the store stands now.</summary>
    /// <remarks>
    /// The session's cache goes with it, so a request finds what the one before it
    /// learned. Public so a test can watch the preview and the cache at work.
    /// </remarks>
    member this.Shapes(cancel: CancellationToken) : ShapeSource =
        { Projection = store.Current
          Preview = this.Preview
          Cancel = cancel
          Cache = shapes }

    /// <summary>What the word at the cursor could become, and the signature it is in.</summary>
    /// <remarks>
    /// Asynchronous because completion may run the stages before the cursor to learn
    /// what flows into it (decision 0031). A host keeps only the newest answer.
    /// </remarks>
    member this.Complete(text: string, cursor: int) : Async<CompletionResult> =
        Completion.complete (this.CompletionRequest(text, cursor))

    /// What the end of the text could become, waited for. For tests, and a host that
    /// cannot await.
    member this.Complete(text: string) : Completion list =
        let text = if isNull text then "" else text
        (Async.RunSynchronously(this.Complete(text, text.Length))).Items

    /// What the token at an offset is, for the page's hover (Phase 8).
    /// A tap is not a keystroke, so it has its own token rather than cancelling the
    /// completion still being worked out for the line.
    member this.Describe(text: string, offset: int) : Async<Hover option> =
        let text = if isNull text then "" else text
        let cancel = CancellationToken.None
        Hover.describe (Completion.request evaluator.Specs (this.Shapes cancel) text offset cancel)

    /// The variables in scope, ordered by name.
    member _.Variables() = store.Current.Variables |> Map.toList

    member _.History() = store.History()
