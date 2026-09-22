/// Failure as a value.
///
/// Decision 0006: no command throws, and no exception crosses a module boundary. A
/// command that cannot do what it was asked returns a fault, the evaluator stops the
/// line, and the session renders the message. Exceptions are for programmer bugs and
/// become a fault of kind `Internal` at the session boundary.
namespace CommandLineReimagined.Core

/// <summary>What sort of failure it is.</summary>
/// <remarks>
/// The kind is additional to the message rather than a replacement for it: every
/// message in the error reference is preserved word for word, because those are what
/// a user reads, and the kind is what a program matches on once `try` binds a fault
/// to a variable in Phase 5.
/// </remarks>
type FaultKind =
    /// The line could not be turned into a tree.
    | Syntax
    /// What was written does not fit what the command declared.
    | Binding
    | UnknownCommand
    /// A path, a variable or a record that is not there.
    | NotFound
    /// Something is already there, or the store would end up inconsistent.
    | Conflict
    /// The request was understood and is not allowed.
    | Invalid
    | Cancelled
    /// A defect. The message names the exception; it is never the user's mistake.
    | Internal

type Fault =
    { Kind: FaultKind
      Message: string
      /// Which stage of the pipeline failed, counted from one. Filled in by the
      /// evaluator, which is the only thing that knows.
      Stage: int option
      Path: string option
      Cause: Fault option }

/// The railway. `Ok` carries the value, `Error` carries the fault.
type Outcome<'T> = Result<'T, Fault>

[<RequireQualifiedAccess>]
module Fault =

    let create kind message =
        { Kind = kind; Message = message; Stage = None; Path = None; Cause = None }

    let withPath path fault = { fault with Path = Some path }

    /// Stamps the stage number on a fault that has not already been given one, so the
    /// innermost failure keeps its own position rather than the outermost overwriting it.
    let atStage stage fault =
        match fault.Stage with
        | Some _ -> fault
        | None -> { fault with Stage = Some stage }

    let causedBy cause fault = { fault with Cause = Some cause }

    // ---------------------------------------------------------------- Parse errors

    let syntax message = create Syntax message
    let couldNotParse () = create Syntax "Could not parse the command."

    // ------------------------------------------------------------- Argument errors

    let needsArgument command parameter =
        create Binding (sprintf "'%s' needs an argument for '%s'." command parameter)

    let tooManyArguments command declared given =
        create
            Binding
            (sprintf
                "'%s' takes %d argument%s, but %d were given."
                command
                declared
                (if declared = 1 then "" else "s")
                given)

    let noArgumentNamed command name =
        create Binding (sprintf "'%s' has no argument named '%s'." command name)

    /// Decision 0017: a command that declares no `Assignments` parameter says so,
    /// rather than ignoring what it was handed.
    let takesNoAssignments command name =
        create Binding (sprintf "'%s' does not take '%s=' assignments." command name)

    let unknownVariable name =
        create NotFound (sprintf "Unknown variable : $%s" name) |> withPath ("$" + name)

    let unsupportedArgument node =
        create Internal (sprintf "Unsupported argument value : %s" node)

    // ------------------------------------------------------------ Expression errors

    /// A node that cannot be one side of a comparison. A tag is the one that happens:
    /// `where $row.x eq <t/>` parses, and there is nothing sensible to compare against.
    let notAnOperand node =
        create Binding (sprintf "A %s cannot be part of an expression." node)

    let unknownOperator op =
        create Internal (sprintf "Unknown operator : %s" op)

    /// Phase 5 gives a parenthesised pipeline a meaning as a value. Until then it parses
    /// and says so, rather than being silently accepted as its own text.
    let nestedPipelineNotAValue text =
        create Invalid (sprintf "A pipeline in parentheses is not a value yet : %s" text)

    /// An expression only means something to a parameter that asked for one, so a
    /// command handed one it cannot use says so rather than comparing display strings.
    let takesNoExpression command parameter =
        create Binding (sprintf "'%s' takes a value for '%s', not an expression." command parameter)

    // ---------------------------------------------------------------- Table errors

    /// Decision 0009: a tag that is not table-shaped names the child that broke the
    /// shape, because "not a table" on its own leaves you counting children by hand.
    let notATable describe index reason =
        create Invalid (sprintf "%s is not a table: child %d %s." describe index reason)

    let needsATable command kind =
        create Binding (sprintf "'%s' needs a table, not %s." command kind)

    let noSuchColumn command name =
        create NotFound (sprintf "'%s' has no column named '%s'." command name)

    // ------------------------------------------------------------ Execution errors

    let unknownCommand name =
        create UnknownCommand (sprintf "Unknown command : %s" name)

    let directoryDoesNotExist path =
        create NotFound (sprintf "Directory does not exist : %s" path) |> withPath path

    let fileDoesNotExist path =
        create NotFound (sprintf "File does not exist : %s" path) |> withPath path

    let isADirectory path =
        create Invalid (sprintf "That is a directory, not a file : %s" path) |> withPath path

    let targetDirectoryExists path =
        create Conflict (sprintf "Target directory already exists : %s" path) |> withPath path

    let targetFileExists path =
        create Conflict (sprintf "Target file already exists : %s" path) |> withPath path

    let targetDirectoryDoesNotExist path =
        create NotFound (sprintf "Target directory does not exist : %s" path) |> withPath path

    let nothingExistsAt path =
        create NotFound (sprintf "Nothing exists at : %s" path) |> withPath path

    let directoryNotEmpty path =
        create Invalid (sprintf "Directory is not empty : %s" path) |> withPath path

    let cannotDeleteCurrentDirectory () =
        create Invalid "Cannot delete the current directory."

    let invalidVariableName name =
        create Invalid (sprintf "'%s' is not a valid variable name." name)

    let setNeedsValue name =
        create Binding (sprintf "'set' needs a value for '%s'." name)

    let stepsMustBePositive () = create Invalid "'steps' must be at least 1."

    let stepsMustBeWhole value =
        create Invalid (sprintf "'steps' must be a whole number, not '%s'." value)

    let notAValidUrl text = create Invalid (sprintf "Not a valid URL : %s" text)

    let noContentLength () = create Invalid "The server did not report a content length."

    let transferEndedEarly () = create Invalid "The transfer ended before all bytes arrived."

    // ----------------------------------------------------------- Evaluation errors

    let cannotEvaluate node = create Internal (sprintf "Cannot evaluate a %s." node)

    let cannotEvaluateChild node = create Internal (sprintf "Cannot evaluate a child %s." node)

    // -------------------------------------------------------------- Script errors

    /// A line in a script says which script and which line, counting every line in the
    /// file including the blank and commented ones, so the number matches an editor's.
    let inScript path line (fault: Fault) =
        { fault with Message = sprintf "%s line %d: %s" path line fault.Message }

    let scriptTooDeep depth =
        create Invalid (sprintf "Scripts are only allowed to run scripts %d deep." depth)

    // ------------------------------------------------------------ Session messages

    let alreadyRunning () = create Invalid "A command is already running. Stop it first."

    let cancelled () = create Cancelled "Stopped."

    /// A defect that reached the session boundary. It is reported rather than swallowed
    /// so that a bug shows up as a message instead of ending the session.
    let internalError (exn: exn) =
        create Internal (sprintf "%s : %s" (exn.GetType().Name) exn.Message)

    // -------------------------------------------------------------- Store messages

    let notInitialised () =
        create Internal "The session has not been initialised. Call Initialize first."

    let nameAlreadyExists path =
        create Conflict (sprintf "Target file already exists : %s" path) |> withPath path

[<RequireQualifiedAccess>]
module Outcome =

    let ok value : Outcome<'T> = Ok value
    let fail fault : Outcome<'T> = Error fault

    let bind (f: 'a -> Outcome<'b>) (outcome: Outcome<'a>) =
        match outcome with
        | Ok value -> f value
        | Error fault -> Error fault

    let map (f: 'a -> 'b) (outcome: Outcome<'a>) =
        match outcome with
        | Ok value -> Ok(f value)
        | Error fault -> Error fault

    let mapFault (f: Fault -> Fault) (outcome: Outcome<'a>) =
        match outcome with
        | Ok value -> Ok value
        | Error fault -> Error(f fault)

    /// The recovery combinator. `else` in the language (decision 0014) is this.
    let orElse (f: Fault -> Outcome<'a>) (outcome: Outcome<'a>) =
        match outcome with
        | Ok value -> Ok value
        | Error fault -> f fault

    let ofOption (fault: Fault) (value: 'a option) =
        match value with
        | Some v -> Ok v
        | Option.None -> Error fault

    let defaultWith (fallback: 'a) (outcome: Outcome<'a>) =
        match outcome with
        | Ok value -> value
        | Error _ -> fallback

    /// Threads a list, stopping at the first fault. Used wherever a command has a list
    /// of things each of which can fail, such as evaluating a tag's attributes.
    let traverse (f: 'a -> Outcome<'b>) (items: 'a list) : Outcome<'b list> =
        let rec loop acc remaining =
            match remaining with
            | [] -> Ok(List.rev acc)
            | head :: tail ->
                match f head with
                | Ok value -> loop (value :: acc) tail
                | Error fault -> Error fault

        loop [] items

    let sequence (items: Outcome<'a> list) : Outcome<'a list> = traverse id items

    /// Runs a side effect only on success, and keeps the outcome either way.
    let iter (f: 'a -> unit) (outcome: Outcome<'a>) =
        match outcome with
        | Ok value -> f value
        | Error _ -> ()

        outcome

type OutcomeBuilder() =
    member _.Bind(outcome, f) = Outcome.bind f outcome
    member _.Return value : Outcome<'T> = Ok value
    member _.ReturnFrom(outcome: Outcome<'T>) = outcome
    member _.Zero() : Outcome<unit> = Ok()
    member _.Delay(f: unit -> Outcome<'T>) = f
    member _.Run(f: unit -> Outcome<'T>) = f ()

    member _.Combine(first: Outcome<unit>, rest: unit -> Outcome<'T>) =
        match first with
        | Ok() -> rest ()
        | Error fault -> Error fault

    member _.TryWith(f: unit -> Outcome<'T>, handler) =
        try
            f ()
        with exn ->
            handler exn

    member _.For(items: seq<'a>, f: 'a -> Outcome<unit>) =
        let rec loop (e: System.Collections.Generic.IEnumerator<'a>) =
            if e.MoveNext() then
                match f e.Current with
                | Ok() -> loop e
                | Error fault -> Error fault
            else
                Ok()

        use enumerator = items.GetEnumerator()
        loop enumerator

/// <summary>
/// An async computation expression over `Outcome`, for commands.
/// </summary>
/// <remarks>
/// A command returns `Async&lt;Outcome&lt;_&gt;&gt;`, so binding a plain outcome inside
/// one otherwise means matching by hand at every step. Both bindings are offered: `let!`
/// on an `Outcome` and on an `Async&lt;Outcome&gt;`.
/// </remarks>
type AsyncOutcomeBuilder() =
    member _.Bind(outcome: Outcome<'a>, f: 'a -> Async<Outcome<'b>>) =
        match outcome with
        | Ok value -> f value
        | Error fault -> async.Return(Error fault)

    member _.Bind(computation: Async<Outcome<'a>>, f: 'a -> Async<Outcome<'b>>) =
        async {
            let! outcome = computation

            match outcome with
            | Ok value -> return! f value
            | Error fault -> return Error fault
        }

    member _.Bind(computation: Async<'a>, f: 'a -> Async<Outcome<'b>>) =
        async {
            let! value = computation
            return! f value
        }

    member _.Return value : Async<Outcome<'T>> = async.Return(Ok value)
    member _.ReturnFrom(computation: Async<Outcome<'T>>) = computation
    member _.ReturnFrom(outcome: Outcome<'T>) = async.Return outcome
    member _.Zero() : Async<Outcome<unit>> = async.Return(Ok())
    member _.Delay(f: unit -> Async<Outcome<'T>>) = async.Delay f

[<AutoOpen>]
module Builders =
    let outcome = OutcomeBuilder()
    let asyncOutcome = AsyncOutcomeBuilder()
