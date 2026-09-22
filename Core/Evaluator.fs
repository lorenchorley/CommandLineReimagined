/// Running a line.
///
/// Decision 0015: one line is one transaction. The stages are folded left to right,
/// threading a value and accumulating events into a working projection so a later
/// stage sees an earlier one's effect. If every stage succeeds the events are
/// committed together; if any fails, nothing is.
namespace CommandLineReimagined.Core

open System
open System.Threading

/// What running a line produced, before the session dresses it up.
type Execution =
    { Value: Value
      /// The transaction, when the line changed something. A read-only line commits
      /// nothing and leaves this empty, which is what keeps `undo` from having to step
      /// over an `ls`.
      Committed: Transaction option }

type Evaluator(commands: Command list, store: Store, blobs: IBlobs) =

    let byName =
        commands
        |> List.map (fun command -> command.Spec.Name.ToLowerInvariant(), command)
        |> Map.ofList

    let unknownCommand =
        commands |> List.tryFind (fun c -> c.Spec.Name = "UnknownCommand")

    let find (name: string) = Map.tryFind (name.ToLowerInvariant()) byName

    member _.Commands = commands

    member _.Specs =
        commands
        |> List.map (fun command -> command.Spec)
        |> List.filter (fun spec -> spec.Name <> "UnknownCommand")
        |> List.sortWith (fun a b -> String.CompareOrdinal(a.Name, b.Name))

    /// <summary>Whether every stage of a line only ever reads (Phase 4).</summary>
    /// <remarks>
    /// Asked of the line before it runs, because a live refresh has to refuse a writing
    /// line rather than discover afterwards that it wrote. A name that resolves to
    /// nothing, and a tag standing alone — which can bind a variable — are both
    /// answered "no": the safe answer is the one that costs a refresh, not a file.
    /// </remarks>
    member _.IsReadOnly(tree: Tree.Node) =
        let reads (name: string) =
            match find name with
            | Some command -> command.Spec.ReadOnly
            | None -> false

        match tree with
        | :? Tree.Empty -> true
        | :? Tree.Pipeline as pipeline ->
            pipeline.OrderedCommands
            |> Seq.forall (fun expression ->
                expression.Expression.Match(
                    (fun (f: Tree.Function) -> reads f.Id.Name),
                    (fun (c: Tree.Cli) -> reads c.Name.Name),
                    (fun (_: Tree.InstanceTag) -> false)))
        | _ -> false

    /// <summary>Runs a parsed line.</summary>
    /// <remarks>
    /// Never raises. A fault is a value all the way out; an unexpected exception from
    /// a command is caught here and becomes a fault of kind `Internal`, so a defect
    /// surfaces as a message rather than ending the session.
    /// </remarks>
    member this.Execute (tree: Tree.Node) (source: string) (output: IOutput) (cancel: CancellationToken) =
        this.Run tree source output cancel true

    /// <summary>Re-reads a line without committing anything (Phase 4).</summary>
    /// <remarks>
    /// What a live view is made of: the page re-runs the listing it is showing whenever
    /// the store changes, and that run must leave no transaction, no scrollback and no
    /// trace in `undo`. A line that names anything but read-only commands is refused
    /// rather than run.
    /// </remarks>
    member this.Refresh (tree: Tree.Node) (source: string) (output: IOutput) (cancel: CancellationToken) =
        if this.IsReadOnly tree then
            this.Run tree source output cancel false
        else
            async.Return(Error(Fault.refreshMustOnlyRead source))

    member private _.Run
        (tree: Tree.Node)
        (source: string)
        (output: IOutput)
        (cancel: CancellationToken)
        (commit: bool)
        =
        async {
            match tree with
            | :? Tree.Empty -> return Ok { Value = Value.Empty; Committed = None }
            | :? Tree.Pipeline as pipeline ->
                let stages = List.ofSeq pipeline.OrderedCommands

                if List.isEmpty stages then
                    return Ok { Value = Value.Empty; Committed = None }
                else

                    /// The projection a stage reads: the committed one with this line's
                    /// events so far folded in.
                    let mutable working = store.Current
                    let mutable pending: Event list = []

                    let record (events: Event list) =
                        pending <- pending @ events
                        working <- Projection.applyAll working events

                    let runCommand (command: Command) (arguments: Tree.Arguments) (input: Value) =
                        async {
                            let scope = Scope working.Variables

                            match Binder.bind arguments command.Spec input scope with
                            | Error fault -> return Error fault
                            | Ok bound ->
                                // The arguments' own events land before the command
                                // runs, so a tag that bound a variable is visible to it.
                                record bound.Events
                                let scope = Scope working.Variables

                                let invocation =
                                    { Spec = command.Spec
                                      Args = bound.Args
                                      Assignments = bound.Assignments
                                      Input = input
                                      Output = output
                                      Scope = scope
                                      Projection = working
                                      Location = working.Location
                                      Blobs = blobs
                                      Cancel = cancel }

                                try
                                    let! result = command.Run invocation

                                    match result with
                                    | Error fault -> return Error fault
                                    | Ok result ->
                                        // Meta commands act on the store directly and
                                        // are outside the transaction, so whatever they
                                        // return is not this line's to commit.
                                        if not command.Spec.Meta then
                                            record result.Events

                                        return Ok result.Value
                                with
                                | :? OperationCanceledException -> return Error(Fault.cancelled ())
                                | exn -> return Error(Fault.internalError exn)
                        }

                    let runStage (input: Value) (expression: Tree.Expression) =
                        async {
                            let named (name: string) (arguments: Tree.Arguments) =
                                async {
                                    match find name with
                                    | Some command -> return! runCommand command arguments input
                                    | None ->
                                        // Reported through a command rather than by
                                        // failing here, so an unknown name renders the
                                        // same way as any other failure.
                                        match unknownCommand with
                                        | Some command ->
                                            let arguments = Tree.Arguments()

                                            arguments.Arguments.Add(
                                                Tree.ArgumentValue(Value = Tree.Identifier(Name = name))
                                            )

                                            return! runCommand command arguments Value.Empty
                                        | None -> return Error(Fault.unknownCommand name)
                                }

                            return!
                                expression.Expression.Match(
                                    (fun (f: Tree.Function) -> named f.Id.Name f.Arguments),
                                    (fun (c: Tree.Cli) -> named c.Name.Name c.Arguments),
                                    (fun (tag: Tree.InstanceTag) ->
                                        async {
                                            let scope = Scope working.Variables

                                            match Binder.evaluateTag scope tag with
                                            | Error fault -> return Error fault
                                            | Ok(value, events) ->
                                                record events
                                                return Ok value
                                        })
                                )
                        }

                    let rec fold (input: Value) stage remaining =
                        async {
                            match remaining with
                            | [] -> return Ok input
                            | expression :: rest ->
                                if cancel.IsCancellationRequested then
                                    return Error(Fault.cancelled ())
                                else
                                    let! result = runStage input expression

                                    match result with
                                    | Error fault -> return Error(Fault.atStage stage fault)
                                    | Ok value -> return! fold value (stage + 1) rest
                        }

                    let! result = fold Value.Empty 1 stages

                    match result with
                    | Error fault ->
                        // Nothing is committed, so a failed line leaves no trace at all
                        // (decision 0015).
                        return Error fault
                    | Ok value when not commit ->
                        // A refresh reads and stops there: the events it gathered — a
                        // read-only line has none — are dropped rather than committed.
                        return Ok { Value = value; Committed = None }
                    | Ok value ->
                        let! committed = store.Commit source pending

                        return committed |> Outcome.map (fun transaction -> { Value = value; Committed = transaction })

            | other -> return Error(Fault.cannotEvaluate (other.GetType().Name))
        }
