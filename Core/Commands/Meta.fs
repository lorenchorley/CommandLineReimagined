/// The commands about the log rather than about the world.
///
/// These are the only ones given a `StoreAccess`, and they are marked `Meta` so the
/// evaluator runs them outside the transaction (decision 0015). A transaction that
/// recorded an undo would have to be undone in turn, and `history` would show the
/// reading of itself.
module CommandLineReimagined.Core.Commands.Meta

open CommandLineReimagined.Core

/// <summary>Nothing to undo is not a failure.</summary>
/// <remarks>
/// It is a plain result, because nothing went wrong: the user asked a question and the
/// answer is "there is nothing". Making it a fault would paint the line red for a
/// state that is perfectly ordinary at the start of a session.
/// </remarks>
let private nothingToUndo = Value.Text "Nothing to undo."

let private nothingToRedo = Value.Text "Nothing to redo."

let undo (store: StoreAccess) =
    { Spec =
        CommandSpec.create "undo" "Reverse the last line that changed something" [ "undo"; "revert"; "back" ] []
        |> CommandSpec.meta
      Run =
        fun _ ->
            async {
                let! result = store.Undo()

                return
                    result
                    |> Outcome.map (fun transaction ->
                        { Value =
                            match transaction with
                            | Some t -> Value.Text($"Undone: {t.Source}")
                            | None -> nothingToUndo
                          Events = [] })
            } }

let redo (store: StoreAccess) =
    { Spec =
        CommandSpec.create "redo" "Put back what undo took away" [ "redo"; "repeat"; "forward" ] []
        |> CommandSpec.meta
      Run =
        fun _ ->
            async {
                let! result = store.Redo()

                return
                    result
                    |> Outcome.map (fun transaction ->
                        { Value =
                            match transaction with
                            | Some t -> Value.Text($"Redone: {t.Source}")
                            | None -> nothingToRedo
                          Events = [] })
            } }

let history (store: StoreAccess) =
    { Spec =
        CommandSpec.create "history" "The lines that changed something, oldest first" [ "history"; "log"; "past" ] []
        |> CommandSpec.meta
      Run =
        fun _ ->
            async {
                let lines =
                    store.History()
                    |> List.map (fun entry ->
                        let time = entry.Transaction.At.ToString "HH:mm:ss"
                        let suffix = if entry.Undone then "  (undone)" else ""
                        Value.Text($"{entry.Transaction.Seq}  {time}  {entry.Transaction.Source}{suffix}"))

                if List.isEmpty lines then
                    return Invocation.pure' (Value.Text "Nothing has happened yet.")
                else
                    return Invocation.pure' (Value.List lines)
            } }

/// <summary>Empties the log and starts again from the seeded filesystem.</summary>
/// <remarks>
/// The escape hatch, for a log that cannot be read back or a session someone wants to
/// start over. It is the one command that cannot be undone, and its description says
/// so, because there is nothing left to undo it from: the transactions that would have
/// been reversed are the ones it threw away.
/// </remarks>
let reset (store: StoreAccess) =
    { Spec =
        CommandSpec.create
            "reset"
            "Empty the log and start again from the seeded filesystem. This cannot be undone"
            [ "reset"; "clear"; "empty"; "start"; "over" ]
            []
        |> CommandSpec.meta
      Run =
        fun _ ->
            async {
                let! seeded = store.Reset()
                let plural = if seeded = 1 then "" else "s"

                let message =
                    if seeded = 0 then
                        "Reset. The filesystem is empty."
                    else
                        sprintf "Reset. %d file%s restored." seeded plural

                return Invocation.pure' (Value.Text message)
            } }

let exit (store: StoreAccess) =
    { Spec =
        CommandSpec.create "exit" "Close the application" [ "quit"; "close"; "stop" ] []
        |> CommandSpec.meta
      Run =
        fun _ ->
            async {
                store.Exit()
                return Invocation.pure' Value.Empty
            } }

/// <summary>Reports a name that resolved to nothing.</summary>
/// <remarks>
/// A command rather than a check in the evaluator, which is how the original shell did
/// it: an unknown name then renders like any other failure, through the same path, and
/// is listed among the commands for the one purpose of being found by name.
/// </remarks>
let unknown =
    { Spec =
        CommandSpec.create
            "UnknownCommand"
            "Reports a command name that could not be resolved"
            []
            [ Parameter.optional "name" "The name that was written" ]
        |> CommandSpec.meta
      Run = fun invocation -> async { return Error(Fault.unknownCommand (Invocation.text "name" invocation)) } }
