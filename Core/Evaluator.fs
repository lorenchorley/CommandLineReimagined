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

/// <summary>Finding the pipelines in parentheses a stage wrote (decision 0023).</summary>
/// <remarks>
/// Only the places the grammar lets one appear: an argument, either side of a
/// comparison, under `and`, `or` and `not`, and after `??`. A tag's attributes take a
/// simple value, so none hides in there. A nested pipeline's own nested pipelines are
/// not collected here; they belong to its stages and run when those do.
/// </remarks>
[<RequireQualifiedAccess>]
module Nesting =

    let rec within (node: Tree.Value) : Tree.NestedPipeline list =
        match node with
        | :? Tree.NestedPipeline as nested -> [ nested ]
        | :? Tree.ComparisonExpression as comparison -> within comparison.Left @ within comparison.Right
        | :? Tree.BooleanExpression as combination -> within combination.Left @ within combination.Right
        | :? Tree.NotExpression as negation -> within negation.Operand
        | _ -> []

    /// The written values of a stage's arguments, in the order written.
    let ofArguments (arguments: Tree.Arguments) : Tree.Value list =
        if isNull (box arguments) then
            []
        else
            arguments.Arguments
            |> Seq.choose (fun (argument: Tree.Argument) ->
                match argument with
                | :? Tree.RequiredArgument as required -> Some required.Value
                | :? Tree.ArgumentValue as value -> Some value.Value
                | :? Tree.NamedArgument as named -> Some named.Value
                | :? Tree.Assignment as assignment -> Some assignment.Value
                | _ -> None)
            |> List.ofSeq

type Evaluator(commands: Command list, store: Store, blobs: IBlobs) =

    /// Every command but the one that reports unknown names. That one is reached when a
    /// name resolves to nothing, never by being typed: typed, it ran with no name and
    /// answered `Unknown command : ` about nothing at all.
    let byName =
        commands
        |> List.filter (fun command -> command.Spec.Name <> "UnknownCommand")
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
    ///
    /// From Phase 5 a line reaches further than its stages: every branch of an `else`,
    /// every pipeline in parentheses and every `??` default is part of it, and any one
    /// of them that writes makes the line one that writes.
    /// </remarks>
    member _.IsReadOnly(tree: Tree.Node) =
        let reads (name: string) =
            match find name with
            | Some command -> command.Spec.ReadOnly
            | None -> false

        let rec pipelineReads (pipeline: Tree.Pipeline) =
            pipeline.OrderedCommands |> Seq.forall stageReads

        and stageReads (expression: Tree.Expression) =
            let own =
                expression.Expression.Match(
                    (fun (f: Tree.Function) -> reads f.Id.Name && argumentsRead f.Arguments),
                    (fun (c: Tree.Cli) -> reads c.Name.Name && argumentsRead c.Arguments),
                    (fun (_: Tree.InstanceTag) -> false),
                    (fun (nested: Tree.NestedPipeline) -> pipelineReads nested.Pipeline),
                    // A variable standing as a stage (decision 0032) only reads it.
                    (fun (_: Tree.VariableReference) -> true))

            own && (isNull expression.Default || nodeReads expression.Default)

        and argumentsRead (arguments: Tree.Arguments) =
            Nesting.ofArguments arguments |> List.forall nodeReads

        and nodeReads (node: Tree.Value) =
            Nesting.within node |> List.forall (fun nested -> pipelineReads nested.Pipeline)

        match tree with
        | :? Tree.Empty -> true
        | :? Tree.Pipeline as pipeline -> pipelineReads pipeline
        | :? Tree.RecoveryLine as line -> line.Pipelines |> Seq.forall pipelineReads
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
            /// The projection a stage reads: the committed one with this line's events
            /// so far folded in.
            let mutable working = store.Current
            let mutable pending: Event list = []

            let record (events: Event list) =
                pending <- pending @ events
                working <- Projection.applyAll working events

            /// <summary>Where the line stood, so a failure can be put back.</summary>
            /// <remarks>
            /// Phase 5: a failed `try` stage and a failed `else` branch leave no trace, the
            /// same way a failed line leaves none (decision 0015). Both the working
            /// projection and the pending events go back, so the stage after a `try`
            /// sees the store as it was before the stage that failed.
            /// </remarks>
            let checkpoint () = working, pending

            let restore (projection, events) =
                working <- projection
                pending <- events

            /// Whether a fault is one the line may recover from. Stop is not: it is the
            /// person at the keyboard saying "no further", and a `try` or an `else`
            /// that carried on regardless would make the Stop button a suggestion.
            let recoverable (fault: Fault) = fault.Kind <> Cancelled

            let rec runCommand (command: Command) (arguments: Tree.Arguments) (input: Value) =
                async {
                    let! resolved = runNested (Nesting.ofArguments arguments)

                    match resolved with
                    | Error fault -> return Error fault
                    | Ok nested ->
                        let scope = Scope working.Variables

                        match Binder.bindWith nested arguments command.Spec input scope with
                        | Error fault -> return Error fault
                        | Ok bound ->
                            // The arguments' own events land before the command runs,
                            // so a tag that bound a variable is visible to it.
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
                                    // Meta commands act on the store directly and are
                                    // outside the transaction, so whatever they return
                                    // is not this line's to commit.
                                    if not command.Spec.Meta then
                                        record result.Events

                                    return Ok result.Value
                            with
                            | :? OperationCanceledException -> return Error(Fault.cancelled ())
                            | exn -> return Error(Fault.internalError exn)
                }

            /// <summary>Runs the pipelines in parentheses a stage wrote, in the order written.</summary>
            /// <remarks>
            /// Decision 0023. Each runs once, with nothing piped into it, in the same
            /// working projection as the line around it, so its events join the line's
            /// transaction and a later stage sees them. The binder then looks each value
            /// up by the node that wrote it; the lookup is by reference, because two
            /// pipelines written the same way are still two pipelines.
            ///
            /// A fault from inside keeps its message and loses its stage number: the
            /// stage that failed, as far as the line is concerned, is the one the
            /// parenthesis is in, and the outer fold stamps that one.
            /// </remarks>
            and runNested (nodes: Tree.Value list) : Async<Outcome<Binder.Nested>> =
                async {
                    let found =
                        System.Collections.Generic.Dictionary<Tree.NestedPipeline, Value>(HashIdentity.Reference)

                    let rec loop remaining =
                        async {
                            match remaining with
                            | [] -> return Ok()
                            | (nested: Tree.NestedPipeline) :: rest ->
                                let! result = runPipeline Value.Empty nested.Pipeline

                                match result with
                                | Error fault -> return Error { fault with Stage = None }
                                | Ok value ->
                                    found[nested] <- value
                                    return! loop rest
                        }

                    let! outcome = loop (nodes |> List.collect Nesting.within)

                    return
                        outcome
                        |> Outcome.map (fun () ->
                            fun (nested: Tree.NestedPipeline) ->
                                match found.TryGetValue nested with
                                | true, value -> Ok value
                                | _ -> Binder.noNested nested)
                }

            /// The value after `??`, evaluated only when it is needed.
            and runDefault (node: Tree.Value) =
                async {
                    let! resolved = runNested [ node ]

                    match resolved with
                    | Error fault -> return Error fault
                    | Ok nested ->
                        match Binder.evaluateWith nested (Scope working.Variables) node with
                        | Error fault -> return Error fault
                        | Ok(value, events) ->
                            record events
                            return Ok value
                }

            and runForm (input: Value) (expression: Tree.Expression) =
                async {
                    let named (name: string) (arguments: Tree.Arguments) =
                        async {
                            match find name with
                            | Some command -> return! runCommand command arguments input
                            | None ->
                                // Reported through a command rather than by failing
                                // here, so an unknown name renders the same way as any
                                // other failure.
                                match unknownCommand with
                                | Some command ->
                                    let arguments = Tree.Arguments()

                                    arguments.Arguments.Add(Tree.ArgumentValue(Value = Tree.Identifier(Name = name)))

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
                                }),
                            // A pipeline standing as a stage is handed the pipe, so
                            // `ls | (where $row.kind eq folder | count)` means what it
                            // would without the parentheses. A fault from inside loses
                            // its stage number, as one from an argument does in
                            // `runNested`: as far as the line is concerned, the stage
                            // that failed is the one the parentheses stand in.
                            (fun (nested: Tree.NestedPipeline) ->
                                async {
                                    let! result = runPipeline input nested.Pipeline
                                    return result |> Outcome.mapFault (fun fault -> { fault with Stage = None })
                                }),
                            // Decision 0032: a variable standing as a stage is its value,
                            // members read off it, so `??`, `else` and `try` treat it as
                            // they treat any stage. Like a tag standing alone, it takes
                            // nothing from the pipe. `$row` outside a predicate is not in
                            // scope, and its lookup says where it does exist.
                            (fun (reference: Tree.VariableReference) ->
                                async {
                                    match Binder.evaluate (Scope working.Variables) reference with
                                    | Error fault -> return Error fault
                                    | Ok(value, events) ->
                                        record events
                                        return Ok value
                                })
                        )
                }

            /// <summary>One stage, with its `try` and its `??` (decision 0014).</summary>
            /// <remarks>
            /// `try` first, because it belongs to the command: a failure becomes a fault
            /// value, stamped with the stage it happened in, and whatever the failed
            /// stage had recorded is put back. `??` then looks at what the stage answered,
            /// and a fault is an answer, so `try x ?? y` keeps the fault.
            /// </remarks>
            and runStage (stage: int) (input: Value) (expression: Tree.Expression) =
                async {
                    let before = checkpoint ()
                    let! result = runForm input expression

                    let result =
                        match result with
                        | Error fault when expression.Try && recoverable fault ->
                            restore before
                            Ok(Value.Fault(Fault.atStage stage fault))
                        | other -> other

                    match result with
                    | Ok value when not (isNull expression.Default) && Value.isAbsent value ->
                        return! runDefault expression.Default
                    | other -> return other
                }

            and runPipeline (input: Value) (pipeline: Tree.Pipeline) : Async<Outcome<Value>> =
                let rec fold (value: Value) stage remaining =
                    async {
                        match remaining with
                        | [] -> return Ok value
                        | expression :: rest ->
                            if cancel.IsCancellationRequested then
                                return Error(Fault.cancelled ())
                            else
                                let! result = runStage stage value expression

                                match result with
                                | Error fault -> return Error(Fault.atStage stage fault)
                                | Ok next -> return! fold next (stage + 1) rest
                    }

                fold input 1 (List.ofSeq pipeline.OrderedCommands)

            /// <summary>Pipelines joined by `else` (decision 0014).</summary>
            /// <remarks>
            /// The first to succeed is the line's value. A branch after a failure starts
            /// from where the line started, with the fault piped in, so the events that
            /// commit are those of the branch that produced the value and no other.
            /// </remarks>
            let runBranches (pipelines: Tree.Pipeline list) =
                let start = checkpoint ()

                let rec loop (previous: Outcome<Value>) remaining =
                    async {
                        match previous, remaining with
                        | Ok value, _ -> return Ok value
                        | Error fault, _ when not (recoverable fault) -> return Error fault
                        | Error fault, [] -> return Error fault
                        | Error fault, (next: Tree.Pipeline) :: rest ->
                            restore start
                            let! result = runPipeline (Value.Fault fault) next
                            return! loop result rest
                    }

                async {
                    match pipelines with
                    | [] -> return Ok Value.Empty
                    | first :: rest ->
                        let! result = runPipeline Value.Empty first
                        return! loop result rest
                }

            let! result =
                match tree with
                | :? Tree.Empty -> async.Return(Ok Value.Empty)
                | :? Tree.Pipeline as pipeline -> runBranches [ pipeline ]
                | :? Tree.RecoveryLine as line -> runBranches (List.ofSeq line.Pipelines)
                | other -> async.Return(Error(Fault.cannotEvaluate (other.GetType().Name)))

            match result with
            | Error fault ->
                // Nothing is committed, so a failed line leaves no trace at all
                // (decision 0015).
                return Error fault
            | Ok value when not commit ->
                // A refresh reads and stops there: the events it gathered — a read-only
                // line has none — are dropped rather than committed.
                return Ok { Value = value; Committed = None }
            | Ok value ->
                let! committed = store.Commit source pending

                return committed |> Outcome.map (fun transaction -> { Value = value; Committed = transaction })
        }
