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
        |> CommandSpec.readOnly
      Run =
        fun _ ->
            async {
                let rows =
                    store.History()
                    |> List.map (fun entry ->
                        [ Value.Number(float entry.Transaction.Seq)
                          Value.Text(entry.Transaction.At.ToString "HH:mm:ss")
                          Value.Text entry.Transaction.Source
                          Value.Boolean entry.Undone
                          // An undo or a redo carries the source of the line it
                          // reverses, so without this `mkdir alpha; undo; redo` reads
                          // as the same line three times.
                          match entry.Transaction.Compensates with
                          | Some seq -> Value.Number(float seq)
                          | None -> Value.None ])

                // A table, so `history | where $row.undone eq true | count` is a
                // question the language can already ask. `compensates` is last so the
                // first four columns keep the places they always had.
                return
                    Invocation.pure' (
                        Value.Table(Table.ofColumns [ "seq"; "at"; "source"; "undone"; "compensates" ] rows)
                    )
            } }

/// <summary>The commands, as a table.</summary>
/// <remarks>
/// `help` used to be a word the page intercepted and answered itself, which meant the
/// desktop shell had no help at all and neither could pipe it. It is a command now, so
/// `help | where $row.name eq set` is an ordinary question. It is `Meta` because
/// reading the command list changes nothing and should leave no transaction.
///
/// The specs arrive as a function rather than a list because the list includes this
/// command, and a value cannot contain itself.
/// </remarks>
let help (specs: unit -> CommandSpec list) =
    { Spec =
        CommandSpec.create
            "help"
            "The commands, with their parameters and what they do"
            [ "help"; "commands"; "what"; "usage"; "manual" ]
            []
        |> CommandSpec.meta
        |> CommandSpec.readOnly
      Run =
        fun _ ->
            async {
                let written (parameter: Parameter) =
                    match parameter.Kind with
                    | Rest -> sprintf "%s..." parameter.Name
                    | _ when parameter.Optional -> sprintf "[%s]" parameter.Name
                    | _ -> sprintf "<%s>" parameter.Name

                let rows =
                    specs ()
                    |> List.map (fun spec ->
                        [ Value.Text spec.Name
                          Value.Text(spec.Parameters |> List.map written |> String.concat " ")
                          Value.Text spec.Description ])

                return Invocation.pure' (Value.Table(Table.ofColumns [ "name"; "parameters"; "description" ] rows))
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

/// How deep a script may run another script. Eight is well past anything sensible and
/// short enough that a cycle says so rather than filling the stack.
let private maximumDepth = 8

/// <summary>Runs a script: one command line per line (decision 0020).</summary>
/// <remarks>
/// Meta, and that is the point: `run` commits nothing of its own, which is what lets
/// each line it runs commit its own transaction. Undo after a script therefore steps
/// back a line at a time, rather than taking the whole file away in one go.
///
/// The depth counter is a cell in the command rather than a field on the invocation
/// because there is one `run` per session and a line runs at a time; a script that
/// runs itself is caught before the stack is.
/// </remarks>
let run (store: StoreAccess) =
    let mutable depth = 0

    { Spec =
        CommandSpec.create
            "run"
            "Run a script: every line in it, as if it had been typed"
            [ "script"; "execute"; "play"; "batch" ]
            [ Parameter.create "path" "The script to run" |> Parameter.piped ]
        |> CommandSpec.meta
      Run =
        fun invocation ->
            async {
                let written = Invocation.text "path" invocation

                match Files.resolve invocation.Projection invocation.Location written with
                | Error fault -> return Error fault
                | Ok record when Record.isFolder record ->
                    return Error(Fault.isADirectory (Files.normalise invocation.Location.Folder written))
                | Ok record when depth >= maximumDepth -> return Error(Fault.scriptTooDeep maximumDepth)
                | Ok record ->
                    let path = Files.pathOf invocation.Projection record

                    let! content =
                        match record.Content with
                        | Some hash -> invocation.Blobs.Get hash
                        | Option.None -> async.Return(Some "")

                    let lines =
                        (defaultArg content "").Replace("\r\n", "\n").Split '\n' |> List.ofArray

                    let rec loop number remaining (last: Value) =
                        async {
                            match remaining with
                            | [] -> return Ok last
                            | (line: string) :: rest ->
                                let written = line.Trim()

                                // Blank and commented lines are skipped and still
                                // counted, so the number in a message is the one an
                                // editor shows.
                                if written = "" || written.StartsWith "#" then
                                    return! loop (number + 1) rest last
                                elif invocation.Cancel.IsCancellationRequested then
                                    return Error(Fault.cancelled ())
                                else
                                    invocation.Output.NewLine().Write("> " + written) |> ignore

                                    let! result = store.RunLine written invocation.Output invocation.Cancel

                                    match result with
                                    | Error fault -> return Error(Fault.inScript path number fault)
                                    | Ok value ->
                                        let text = Value.display value

                                        if text <> "" then
                                            invocation.Output.NewLine().Write text |> ignore

                                        return! loop (number + 1) rest value
                        }

                    depth <- depth + 1

                    try
                        let! result = loop 1 lines Value.Empty
                        return result |> Outcome.map (fun value -> { Value = value; Events = [] })
                    finally
                        depth <- depth - 1
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
