/// From what was written to what the command is given.
///
/// Two jobs: evaluating a parsed value to a runtime value, and matching written
/// arguments against declared parameters. The matching algorithm is the one in the
/// execution model specification, unchanged, with assignments added (decision 0017).
namespace CommandLineReimagined.Core

open System

/// What binding produced.
type Bound =
    { Args: Map<string, Value>
      /// Every `name=value` on the line, in the order written.
      Assignments: (string * Value) list
      /// <summary>What evaluating the arguments changed.</summary>
      /// <remarks>
      /// A tag can name a variable — `<size|dimension value=3/>` leaves `$size` bound —
      /// and binding a variable is a change to the store like any other. The old
      /// implementation mutated the scope in place, which is the kind of invisible
      /// effect decision 0010 set out to remove, so the binding comes back as an event
      /// and is committed, and undone, with the rest of the line.
      /// </remarks>
      Events: Event list }

[<RequireQualifiedAccess>]
module Binder =

    /// A value, what producing it changed, and the scope afterwards. The scope is
    /// threaded rather than mutated so that a tag which binds a variable is visible to
    /// the arguments written after it.
    type private Step = (Value * Event list) * Scope

    let private lookup (scope: Scope) (name: string) : Outcome<Value> =
        scope.TryFind name |> Outcome.ofOption (Fault.unknownVariable name)

    /// <summary>What a nested pipeline produced, looked up by the node that wrote it.</summary>
    /// <remarks>
    /// Running a pipeline is asynchronous and belongs to the evaluator, and binding is
    /// neither, so the evaluator runs a stage's nested pipelines first and hands the
    /// binder their values through this (decision 0023).
    /// </remarks>
    type Nested = Tree.NestedPipeline -> Outcome<Value>

    /// For a line that has no nested pipelines, or none that were run.
    let noNested: Nested =
        fun pipeline -> Error(Fault.nestedPipelineNotAValue (Value.exprText (Expr.Nested pipeline.Pipeline)))

    let rec private eval (nested: Nested) (scope: Scope) (node: Tree.Value) : Outcome<Step> =
        let evalTag = evalTag nested

        match node with
        | null -> Ok((Value.Empty, []), scope)

        | :? Tree.StringConstant as text -> Ok((Value.Text text.Value, []), scope)

        // The grammar cannot tell a name from a path from a number: all three are
        // words. This is where the difference is decided.
        | :? Tree.Identifier as identifier -> Ok((Value.ofWord identifier.Name, []), scope)

        | :? Tree.VariableReference as reference ->
            // `$row.size` reads a member off the value, which is the same notation
            // inside a predicate and outside one (decision 0008).
            lookup scope reference.Name.Name
            |> Outcome.map (fun value ->
                let read =
                    reference.Members
                    |> Seq.fold (fun current (m: Tree.MemberName) -> Expr.readMember m.Name current) value

                ((read, []), scope))

        | :? Tree.TagValue as tag -> evalTag scope tag.Tag

        | :? Tree.NestedPipeline as pipeline -> nested pipeline |> Outcome.map (fun value -> ((value, []), scope))

        | :? Tree.Constant as constant -> Ok((Value.Text(string constant), []), scope)

        | other -> Error(Fault.unsupportedArgument (other.GetType().Name))

    and private evalTag (nested: Nested) (scope: Scope) (tag: Tree.InstanceTag) : Outcome<Step> =
        let buildTag = buildTag nested

        match tag with
        | :? Tree.ObjectInstance as instance ->
            buildTag scope instance.ObjectType.Value instance.Attributes instance.Children
            |> Outcome.map (fun ((built, events), scope) ->
                bindNamed scope instance.VariableName (Value.Object built) events)

        | :? Tree.ComponentInstance as instance ->
            buildTag scope instance.ComponentType.Value instance.Attributes instance.Children
            |> Outcome.map (fun ((built, events), scope) ->
                bindNamed scope instance.VariableName (Value.Component built) events)

        // `<$name>` reads a variable back.
        | :? Tree.VariableTag as reference ->
            lookup scope reference.Name.Name |> Outcome.map (fun v -> ((v, []), scope))

        | other -> Error(Fault.cannotEvaluate (other.GetType().Name))

    and private buildTag
        (nested: Nested)
        (scope: Scope)
        (typeName: string)
        (attributes: Tree.TagAttributeList)
        (children: Tree.TagList)
        : Outcome<(Tag * Event list) * Scope> =

        let writtenAttributes =
            if isNull (box attributes) then [] else List.ofSeq attributes.Attributes

        let writtenChildren =
            if isNull (box children) then [] else List.ofSeq children.Tags

        let rec attributeLoop (scope: Scope) acc events remaining =
            match remaining with
            | [] -> Ok((List.rev acc, events), scope)
            | (attribute: Tree.TagAttribute) :: rest ->
                match eval nested scope attribute.Value with
                | Error fault -> Error fault
                | Ok((value, produced), scope) ->
                    attributeLoop scope ((attribute.Name.Name, value) :: acc) (events @ produced) rest

        let rec childLoop (scope: Scope) acc events remaining =
            match remaining with
            | [] -> Ok((List.rev acc, events), scope)
            | (child: Tree.Tag) :: rest ->
                match child with
                | :? Tree.InstanceTag as child ->
                    match evalTag nested scope child with
                    | Error fault -> Error fault
                    | Ok((value, produced), scope) -> childLoop scope (value :: acc) (events @ produced) rest
                | other -> Error(Fault.cannotEvaluateChild (other.GetType().Name))

        outcome {
            let! (pairs, attributeEvents), scope = attributeLoop scope [] [] writtenAttributes
            let! (values, childEvents), scope = childLoop scope [] attributeEvents writtenChildren

            // Built in the order it was written, so `<note name=monday mood=good/>`
            // reads back the way it was typed rather than alphabetically.
            let tag = Tag.create typeName pairs values

            return ((tag, childEvents), scope)
        }

    /// `<name|type/>` leaves `$name` bound.
    and private bindNamed (scope: Scope) (name: Tree.VariableName) (value: Value) (events: Event list) : Step =
        if isNull (box name) then
            ((value, events), scope)
        else
            let before = scope.TryFind name.Name
            ((value, events @ [ VariableChanged(name.Name, before, Some value) ]), scope.Bind name.Name value)

    /// Evaluates one written value against a scope.
    let evaluateWith (nested: Nested) (scope: Scope) (node: Tree.Value) : Outcome<Value * Event list> =
        eval nested scope node |> Outcome.map fst

    /// Evaluates one written value against a scope, where no nested pipeline has run.
    let evaluate (scope: Scope) (node: Tree.Value) = evaluateWith noNested scope node

    /// Evaluates a tag standing alone as a pipeline stage.
    let evaluateTag (scope: Scope) (tag: Tree.InstanceTag) : Outcome<Value * Event list> =
        evalTag noNested scope tag |> Outcome.map fst

    // --------------------------------------------------------------------- Binding

    let private findNamed (spec: CommandSpec) (name: string) =
        spec.Parameters
        |> List.tryFind (fun parameter ->
            String.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase)
            || (match parameter.Flag with
                | Some flag -> String.Equals(flag, name, StringComparison.OrdinalIgnoreCase)
                | None -> false))

    /// An argument that carries a value rather than naming one.
    let private plainValue (argument: Tree.Argument) =
        match argument with
        | :? Tree.RequiredArgument as required -> Some required.Value
        | :? Tree.ArgumentValue as value -> Some value.Value
        | _ -> None

    let private writtenValue (argument: Tree.Argument) : Outcome<Tree.Value> =
        match plainValue argument with
        | Some value -> Ok value
        | None ->
            match argument with
            | :? Tree.NamedArgument as named -> Ok named.Value
            | other -> Error(Fault.unsupportedArgument (other.GetType().Name))

    /// <summary>Binds one written argument to one declared parameter.</summary>
    /// <remarks>
    /// Where the two differ is the parameter's kind. A `Predicate` parameter is handed
    /// the expression unevaluated, because it is the command that knows what to bind
    /// `$row` to; every other kind is evaluated here, and an argument that actually
    /// wrote an operator is a binding fault rather than a comparison of display strings.
    /// </remarks>
    let private bindArgument (nested: Nested) (spec: CommandSpec) (parameter: Parameter) (scope: Scope) (node: Tree.Value) =
        match parameter.Kind with
        | Predicate ->
            // A nested pipeline in a predicate ran once, before binding, so the
            // predicate compares every row against the one value it produced.
            let constant (pipeline: Tree.NestedPipeline) = nested pipeline |> Outcome.map Expr.Const

            // Decision 0033: a predicate that never reads `$row` is the same for every
            // row. It is checked as written, before any nested pipeline's value is put
            // in, so the fault quotes what was typed.
            Expr.ofNode node
            |> Outcome.bind Expr.asksAboutTheRow
            |> Outcome.bind (fun _ -> Expr.ofNodeWith constant node)
            |> Outcome.map (fun expr -> ((Value.Query expr, []), scope))
        | _ when Expr.isExpression node -> Error(Fault.takesNoExpression spec.Name parameter.Name)
        | _ -> eval nested scope node

    /// The running state of step 1, so that four things do not have to be threaded as
    /// a tuple through every branch.
    type private Walk =
        { Named: (string * Value) list
          Positional: Tree.Argument list
          Assignments: (string * Value) list
          Events: Event list
          Scope: Scope }

    /// <summary>Matches what was written against what the command declared.</summary>
    /// <remarks>
    /// Steps 1 to 4 of the execution model. What is new is that evaluating an argument
    /// can produce events, so the scope and the events are threaded through rather
    /// than a dictionary being filled in place.
    /// </remarks>
    let bindWith (nested: Nested) (arguments: Tree.Arguments) (spec: CommandSpec) (input: Value) (scope: Scope) : Outcome<Bound> =
        let bindArgument = bindArgument nested
        let eval = eval nested

        let written =
            if isNull (box arguments) then [||] else Array.ofSeq arguments.Arguments

        // Step 1: named arguments, flags and assignments, left to right. Anything else
        // joins the positional queue in the order it was written.
        let rec step1 (state: Walk) index : Outcome<Walk> =
            if index >= written.Length then
                Ok
                    { state with
                        Named = List.rev state.Named
                        Positional = List.rev state.Positional
                        Assignments = List.rev state.Assignments }
            else
                match written[index] with
                | :? Tree.NamedArgument as named ->
                    let name = named.Name.Match((fun flag -> flag.Name), (fun identifier -> identifier.Name))

                    match findNamed spec name with
                    | None -> Error(Fault.noArgumentNamed spec.Name name)
                    | Some parameter ->
                        bindArgument spec parameter state.Scope named.Value
                        |> Outcome.bind (fun ((value, produced), scope) ->
                            step1
                                { state with
                                    Named = (parameter.Name, value) :: state.Named
                                    Events = state.Events @ produced
                                    Scope = scope }
                                (index + 1))

                | :? Tree.Flag as flag ->
                    match findNamed spec flag.Name with
                    | None -> Error(Fault.noArgumentNamed spec.Name flag.Name)
                    | Some parameter ->
                        // `-flag value` takes the next argument when there is a plain
                        // one after it; a flag with nothing after it is a switch.
                        let next =
                            if index + 1 < written.Length then plainValue written[index + 1] else None

                        match next with
                        | Some node ->
                            bindArgument spec parameter state.Scope node
                            |> Outcome.bind (fun ((value, produced), scope) ->
                                step1
                                    { state with
                                        Named = (parameter.Name, value) :: state.Named
                                        Events = state.Events @ produced
                                        Scope = scope }
                                    (index + 2))
                        | None ->
                            step1
                                { state with Named = (parameter.Name, Value.Boolean true) :: state.Named }
                                (index + 1)

                | :? Tree.Assignment as assignment ->
                    // Decision 0017: data, not parameter binding. A command that takes
                    // none says so rather than ignoring what it was handed.
                    match CommandSpec.assignmentParameter spec with
                    | None -> Error(Fault.takesNoAssignments spec.Name assignment.Name.Name)
                    | Some _ ->
                        eval state.Scope assignment.Value
                        |> Outcome.bind (fun ((value, produced), scope) ->
                            step1
                                { state with
                                    Assignments = (assignment.Name.Name, value) :: state.Assignments
                                    Events = state.Events @ produced
                                    Scope = scope }
                                (index + 1))

                | other -> step1 { state with Positional = other :: state.Positional } (index + 1)

        // Step 2: each declared parameter in order takes the next positional argument,
        // then the pipe, then its default.
        let rec step2 (state: Walk) (args: Map<string, Value>) parameters : Outcome<Walk * Map<string, Value>> =
            match parameters with
            | [] -> Ok(state, args)
            | (parameter: Parameter) :: rest ->
                if Map.containsKey parameter.Name args then
                    step2 state args rest
                elif parameter.Kind = Rest then
                    // Greedy, and never takes the pipe: on a table function the pipe is
                    // the table, and a rest parameter that swallowed it would leave the
                    // table unbound (decision 0021).
                    let rec collect (state: Walk) collected remaining =
                        match remaining with
                        | [] -> Ok(state, List.rev collected)
                        | argument :: rest ->
                            writtenValue argument
                            |> Outcome.bind (bindArgument spec parameter state.Scope)
                            |> Outcome.bind (fun ((value, produced), scope) ->
                                collect
                                    { state with
                                        Positional = []
                                        Events = state.Events @ produced
                                        Scope = scope }
                                    (value :: collected)
                                    rest)

                    collect state [] state.Positional
                    |> Outcome.bind (fun (state, collected) ->
                        step2 state (Map.add parameter.Name (Value.List collected) args) rest)
                else
                    match state.Positional with
                    | argument :: remaining ->
                        writtenValue argument
                        |> Outcome.bind (bindArgument spec parameter state.Scope)
                        |> Outcome.bind (fun ((value, produced), scope) ->
                            step2
                                { state with
                                    Positional = remaining
                                    Events = state.Events @ produced
                                    Scope = scope }
                                (Map.add parameter.Name value args)
                                rest)
                    | [] ->
                        if parameter.AcceptsPipe && not (Value.isAbsent input) then
                            step2 state (Map.add parameter.Name input args) rest
                        elif parameter.Optional then
                            step2 state (Map.add parameter.Name parameter.Default args) rest
                        else
                            Error(Fault.needsArgument spec.Name parameter.Name)

        outcome {
            let start =
                { Named = []
                  Positional = []
                  Assignments = []
                  Events = []
                  Scope = scope }

            let! walked = step1 start 0
            let! state, args = step2 walked (Map.ofList walked.Named) (CommandSpec.positional spec)

            // Step 3: anything left over is more than the command takes. The count
            // names what was supplied rather than what remains, which used to read as
            // "takes 1 argument, but 1 more were given".
            if not (List.isEmpty state.Positional) then
                let declared = List.length (CommandSpec.positional spec)
                return! Error(Fault.tooManyArguments spec.Name declared (declared + List.length state.Positional))
            else
                return
                    { Args = args
                      Assignments = state.Assignments
                      Events = state.Events }
        }

    /// Binds a command's arguments where no nested pipeline has run.
    let bind (arguments: Tree.Arguments) (spec: CommandSpec) (input: Value) (scope: Scope) : Outcome<Bound> =
        bindWith noNested arguments spec input scope
