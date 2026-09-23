/// The value commands: echo, set, vars and is-fault.
module CommandLineReimagined.Core.Commands.Values

open CommandLineReimagined.Core

let echo =
    { Spec =
        CommandSpec.create
            "echo"
            "Writes its argument, or whatever was piped into it"
            [ "print"; "write"; "output" ]
            [ Parameter.create "text" "What to write" |> Parameter.piped |> Parameter.takes Takes.Value ]
        |> CommandSpec.readOnly
      Run = fun invocation -> async { return Invocation.pure' (Invocation.value "text" invocation) } }

/// <summary>Binds a value to a name.</summary>
/// <remarks>
/// The binding is an event now, which is what fixes the defect decision 0010 was
/// written for: `set v 1`, `set v 2`, undo, undo used to leave `$v` at 1, because the
/// command instance that held the undo state was shared and replayed the wrong one.
/// There is no undo state to get wrong any more — the event carries what was there
/// before, and the store inverts it.
/// </remarks>
let set =
    { Spec =
        CommandSpec.create
            "set"
            "Bind a value, or whatever was piped in, to a variable"
            [ "set"; "variable"; "assign"; "bind"; "let" ]
            [ Parameter.create "name" "The variable's name, without the $" |> Parameter.takes Takes.VariableName
              Parameter.create "value" "The value" |> Parameter.piped |> Parameter.takes Takes.Value ]
      Run =
        fun invocation ->
            async {
                let name = Invocation.text "name" invocation

                if not (Scope.IsValidName name) then
                    return Error(Fault.invalidVariableName name)
                else
                    let value = Invocation.value "value" invocation

                    if Value.isAbsent value then
                        return Error(Fault.setNeedsValue name)
                    else
                        let before = invocation.Scope.TryFind name
                        return Invocation.withEvents value [ VariableChanged(name, before, Some value) ]
            } }

let vars =
    { Spec =
        CommandSpec.create "vars" "List the variables in scope" [ "variables"; "list"; "show"; "scope" ] []
        |> CommandSpec.readOnly
      Run =
        fun invocation ->
            async {
                let variables = invocation.Scope.All()

                // Empty still answers a table, so `vars | count` is 0 rather than a
                // fault. The hint is written beside it, because an empty table on its
                // own does not say what to do next.
                if List.isEmpty variables then
                    invocation.Output.NewLine().Write("No variables. Try: set greeting hello") |> ignore

                let rows = variables |> List.map (fun (name, value) -> [ Value.Text name; value ])

                return Invocation.pure' (Value.Table(Table.ofColumns [ "name"; "value" ] rows))
            } }

/// <summary>Whether a value is a fault (Phase 5).</summary>
/// <remarks>
/// `try` makes a failure into a value, and a value can be anything, so a script that
/// wants to branch on "did that work" needs a question it can ask without knowing what
/// the success would have looked like. Given nothing at all it answers `false`: nothing
/// is not a failure.
/// </remarks>
let isFault =
    { Spec =
        CommandSpec.create
            "is-fault"
            "Whether a value, or whatever was piped in, is a fault that try caught"
            [ "fault"; "failed"; "error"; "check"; "try" ]
            [ Parameter.optional "value" "The value to ask about" |> Parameter.piped |> Parameter.takes Takes.Value ]
        |> CommandSpec.readOnly
      Run =
        fun invocation ->
            async {
                let answer =
                    match Invocation.value "value" invocation with
                    | Value.Fault _ -> true
                    | _ -> false

                return Invocation.pure' (Value.Boolean answer)
            } }
