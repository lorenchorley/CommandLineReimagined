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

/// <summary>What a parameter's argument is, for completion and the signature hint (Phase 8).</summary>
/// <remarks>
/// Declared on the parameter rather than kept in a second list beside the commands, so
/// a command's own declaration is the one place that says what `sort`'s first word
/// can be. It changes nothing about binding: the binder never reads it.
/// </remarks>
[<RequireQualifiedAccess>]
type Takes =
    /// Files and folders: what every argument was offered before Phase 8.
    | Anything
    | Path
    /// Somewhere to be: a folder or a saved view.
    | Place
    /// A name that does not exist yet, so there is nothing to offer.
    | NewName
    | Url
    /// A column of the table flowing into the stage.
    | Column
    | Count
    | Number
    | Text
    /// A word that turns something on, and the word for off when there is one.
    | Switch of on: string * off: string option
    | VariableName
    | CommandName
    /// Any value: the variables in scope are the things worth offering.
    | Value

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
      Kind: ParamKind
      /// What the argument is, for completion. `Anything` unless the command says.
      Takes: Takes }

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
      Meta: bool
      /// <summary>Whether the command can only ever read (Phase 4).</summary>
      /// <remarks>
      /// Declared rather than discovered, because a live view re-runs a line behind the
      /// user's back and has to refuse a writing one *before* it runs: by the time the
      /// events are in hand it is too late to say no. `attr` is deliberately not marked,
      /// because the same command reads with no assignments and writes with them.
      /// </remarks>
      ReadOnly: bool }

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
      /// in, so `mkdir a | in a` works. It is not what is committed; that happens once
      /// the whole line has succeeded.
      /// </remarks>
      Projection: Projection
      Location: Location
      Blobs: IBlobs
      Cancel: CancellationToken }

/// What a command answers with: a value for the next stage, what it changed, and what
/// the terminal should say about it beyond the value, such as why a filter kept
/// nothing (decision 0043). Notes are guidance, never part of the value.
type CommandResult =
    { Value: Value
      Events: Event list
      Notes: Note list }

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
          Kind = Single
          Takes = Takes.Anything }

    /// Optional with no default beyond `Empty`, which is what a command checks for
    /// when it wants to know whether anything was written.
    let optional name description =
        { create name description with Optional = true }

    let withDefault value parameter =
        { parameter with Optional = true; Default = value }

    let piped parameter = { parameter with AcceptsPipe = true }

    let withFlag flag parameter = { parameter with Flag = Some flag }

    /// Says what the argument is, so completion can offer the right things for it.
    let takes (what: Takes) (parameter: Parameter) = { parameter with Takes = what }

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
          Meta = false
          ReadOnly = false }

    let meta spec = { spec with Meta = true }

    /// A command that answers a question and changes nothing, so a live view may
    /// re-run it.
    let readOnly spec = { spec with ReadOnly = true }

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

    /// <summary>A switch, and whether it is on.</summary>
    /// <remarks>
    /// Written bare, `-desc` binds `true`, and not written at all it is off. Written as
    /// a word it is on only for the switch's own name, so `sort name desc` reads as it
    /// looks, and off for `off`, the word a command offers for the other way (`asc`).
    /// Any other word is a fault naming what the switch takes: it used to count as on,
    /// so `sort name asc` sorted downwards.
    /// </remarks>
    let switch (off: string option) (name: string) (invocation: Invocation) : Outcome<bool> =
        let own =
            invocation.Spec.Parameters
            |> List.tryFind (fun p -> p.Name = name)
            |> Option.bind (fun p -> p.Flag)
            |> Option.defaultValue name

        let reads (expected: string) (written: string) =
            System.String.Equals(written, expected, System.StringComparison.OrdinalIgnoreCase)

        match Map.tryFind name invocation.Args with
        | Some(Value.Boolean b) -> Ok b
        | Some v when not (Value.isAbsent v) ->
            let written = Value.display v

            if reads own written then Ok true
            elif off |> Option.exists (fun word -> reads word written) then Ok false
            else Error(Fault.notASwitchValue invocation.Spec.Name name (own :: Option.toList off) written)
        | _ -> Ok false

    /// A switch with no word for off: written bare or as its own name, it is on.
    let flag (name: string) (invocation: Invocation) = switch None name invocation

    /// A result that changed nothing, which is most of them.
    let pure' (value: Value) = Ok { Value = value; Events = []; Notes = [] }

    let withEvents (value: Value) (events: Event list) = Ok { Value = value; Events = events; Notes = [] }

    /// Adds notes to a result that succeeded, after any it already carries. A failed
    /// one keeps its fault as it is: a fault's notes are the fault's own.
    let withNotes (notes: Note list) (result: Outcome<CommandResult>) : Outcome<CommandResult> =
        result |> Result.map (fun r -> { r with Notes = r.Notes @ notes })
