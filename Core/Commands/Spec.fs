/// What a command is.
///
/// Decision 0006 and 0010 together: a command is a function from an invocation to a
/// value and a list of events. It reads the projection and never touches the store, so
/// running one twice over the same projection gives the same answer twice, and undo is
/// the store's business rather than every command's.
namespace CommandLineReimagined.Core

open System.Threading

/// How an argument reaches a parameter.
type ParamKind =
    /// One value, positionally or by name.
    | Single
    /// <summary>An expression, handed over unevaluated.</summary>
    /// <remarks>
    /// A predicate is evaluated once per row, in a scope with `$row` bound to that row
    /// (decision 0008), so the command runs it rather than the binder.
    /// </remarks>
    | Predicate
    /// <summary>Every remaining positional argument, as a `List` (decision 0021).</summary>
    /// <remarks>
    /// Greedy, so it must be the last parameter that can take a positional argument. A
    /// command that declares one owns its own arity message, because "too many
    /// arguments" can never happen to it.
    /// </remarks>
    | Rest
    /// Every `name=value` written on the line, in order (decision 0017).
    | Assignments

type Parameter =
    { Name: string
      Description: string
      Optional: bool
      /// The short name a `-flag` matches, when it differs from `Name`.
      Flag: string option
      Default: Value
      /// Whether this parameter takes the previous stage's value when nothing was
      /// written for it. At most one parameter should, or a pipe becomes ambiguous.
      AcceptsPipe: bool
      Kind: ParamKind }

type CommandSpec =
    { Name: string
      Description: string
      Keywords: string list
      /// In the order positional arguments fill them.
      Parameters: Parameter list
      /// <summary>Whether the command is about the log rather than the world.</summary>
      /// <remarks>
      /// `undo`, `redo`, `history` and `exit` run outside the transaction
      /// (decision 0015). A transaction that recorded an undo would have to be undone
      /// in turn, and `history` would show the reading of itself.
      /// </remarks>
      Meta: bool }

/// Where a command writes while it is still running.
///
/// A command's result is its return value; this is for what has to appear *during*
/// execution, such as a progress bar, which cannot wait to be returned.
type IOutput =
    abstract NewLine: unit -> IOutputLine

/// A written run of text that can still be changed, for progress indicators.
and IOutputText =
    abstract Text: string with get, set

and IOutputLine =
    abstract Write: text: string -> IOutputText

/// <summary>Content-addressed storage, as much of it as a command may see.</summary>
/// <remarks>
/// A command needs to read and write file content, and content lives in the log beside
/// the transactions. This is the whole of the log a command is given: it can put text
/// and get text, and it cannot append, undo or read the history. `write` puts its text
/// and returns a hash in an event; the store never has to hold the text itself.
/// </remarks>
type IBlobs =
    abstract Put: string -> Async<Hash>
    abstract Get: Hash -> Async<string option>

/// <summary>The store, as much of it as a meta command may see.</summary>
/// <remarks>
/// A capability rather than the store itself, so that `undo` can undo without every
/// other command being able to. It is passed to the four commands that need it when
/// they are built, and there is no way for the others to reach it.
/// </remarks>
type StoreAccess =
    { Undo: unit -> Async<Outcome<Transaction option>>
      Redo: unit -> Async<Outcome<Transaction option>>
      History: unit -> HistoryEntry list
      /// Empties the log and seeds it again. Not undoable; see `reset`.
      Reset: unit -> Async<int>
      /// <summary>Runs one line as if it had been typed.</summary>
      /// <remarks>
      /// Decision 0020: a script's lines each commit their own transaction, so `run`
      /// cannot run them itself — it has no store — and this is the whole of what it is
      /// given. Only `run` is handed a `StoreAccess`, so only `run` can reach it.
      /// </remarks>
      RunLine: string -> IOutput -> CancellationToken -> Async<Outcome<Value>>
      /// What shutting down means is the host's business. The browser has nothing to
      /// shut down and passes a function that does nothing.
      Exit: unit -> unit }

/// Everything one execution of one command is given.
type Invocation =
    { Spec: CommandSpec
      Args: Map<string, Value>
      /// Every `name=value` on the line, in the order they were written, for a command
      /// that declared an `Assignments` parameter.
      Assignments: (string * Value) list
      /// The previous stage's value, or `Empty` at the head of a pipeline.
      Input: Value
      Output: IOutput
      Scope: Scope
      /// <summary>The state the command reads.</summary>
      /// <remarks>
      /// The *working* projection, which already has this line's earlier stages folded
      /// in, so `mkdir a | cd a` works. It is not what is committed; that happens once
      /// the whole line has succeeded.
      /// </remarks>
      Projection: Projection
      Location: Location
      Blobs: IBlobs
      Cancel: CancellationToken }

/// What a command answers with: a value for the next stage, and what it changed.
type CommandResult = { Value: Value; Events: Event list }

type Command =
    { Spec: CommandSpec
      Run: Invocation -> Async<Outcome<CommandResult>> }

[<RequireQualifiedAccess>]
module Parameter =

    let create name description =
        { Name = name
          Description = description
          Optional = false
          Flag = None
          Default = Value.Empty
          AcceptsPipe = false
          Kind = Single }

    /// Optional with no default beyond `Empty`, which is what a command checks for
    /// when it wants to know whether anything was written.
    let optional name description =
        { create name description with Optional = true }

    let withDefault value parameter =
        { parameter with Optional = true; Default = value }

    let piped parameter = { parameter with AcceptsPipe = true }

    let withFlag flag parameter = { parameter with Flag = Some flag }

    /// The one parameter that collects `name=value` arguments.
    let assignments name description =
        { create name description with Kind = Assignments; Optional = true }

    /// A parameter that takes an expression and evaluates it itself, per row.
    let predicate name description =
        { create name description with Kind = Predicate }

    /// The one parameter that collects every remaining positional argument. It is
    /// optional because "none left" is the empty list, not a missing argument.
    let rest name description =
        { create name description with Kind = Rest; Optional = true; Default = Value.List [] }

[<RequireQualifiedAccess>]
module CommandSpec =

    let create name description keywords parameters =
        { Name = name
          Description = description
          Keywords = keywords
          Parameters = parameters
          Meta = false }

    let meta spec = { spec with Meta = true }

    /// The parameters positional arguments can fill: everything but the assignment
    /// collector, which is filled by a notation of its own.
    let positional (spec: CommandSpec) =
        spec.Parameters |> List.filter (fun p -> p.Kind <> Assignments)

    let assignmentParameter (spec: CommandSpec) =
        spec.Parameters |> List.tryFind (fun p -> p.Kind = Assignments)

    let takesRest (spec: CommandSpec) =
        spec.Parameters |> List.exists (fun p -> p.Kind = Rest)

[<RequireQualifiedAccess>]
module Invocation =

    /// The value written for a parameter. The binder has already checked that required
    /// parameters are present, so a command that declared one can ask without guarding.
    let value (name: string) (invocation: Invocation) =
        match Map.tryFind name invocation.Args with
        | Some value -> value
        | None -> Value.Empty

    let text (name: string) (invocation: Invocation) = value name invocation |> Value.argument

    /// Whether anything was actually written or piped for a parameter, as opposed to
    /// the parameter falling back to an empty default.
    let given (name: string) (invocation: Invocation) =
        match Map.tryFind name invocation.Args with
        | Some v -> not (Value.isAbsent v)
        | None -> false

    let textOr (fallback: string) (name: string) (invocation: Invocation) =
        if given name invocation then text name invocation else fallback

    /// The values a `Rest` parameter collected, or the empty list.
    let list (name: string) (invocation: Invocation) =
        match Map.tryFind name invocation.Args with
        | Some(Value.List items) -> items
        | Some value when not (Value.isAbsent value) -> [ value ]
        | _ -> []

    /// The predicate a `Predicate` parameter was handed, unevaluated.
    let predicate (name: string) (invocation: Invocation) =
        match Map.tryFind name invocation.Args with
        | Some(Value.Query expr) -> Some expr
        | _ -> None

    /// A flag written bare binds `true`; a flag not written at all is absent.
    let flag (name: string) (invocation: Invocation) =
        match Map.tryFind name invocation.Args with
        | Some(Value.Boolean b) -> b
        | Some v -> not (Value.isAbsent v)
        | None -> false

    /// A result that changed nothing, which is most of them.
    let pure' (value: Value) = Ok { Value = value; Events = [] }

    let withEvents (value: Value) (events: Event list) = Ok { Value = value; Events = events }
