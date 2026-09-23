/// Where the cursor is, in the terms the language uses (decision 0031).
///
/// Completion used to guess from the text: the last word, whether a `$` came earlier
/// in the stage. This asks the parser instead. The word under the cursor is replaced
/// by a placeholder no one will type, whatever the line left open is closed, and the
/// tree that comes back says which stage, command, parameter and part of an expression
/// the placeholder sits in. The providers each answer one kind of place.
namespace CommandLineReimagined.Core

open System
open CommandLineReimagined.Parsing

/// The stage the cursor is in.
type Stage =
    { /// The command, when the name is one.
      Spec: CommandSpec option
      /// The name as written, which is all there is when `Spec` is `None`.
      Name: string
      /// Its position in its pipeline, from 0.
      Index: int
      /// <summary>The stages before it, written out as a line of their own.</summary>
      /// <remarks>
      /// What `Shape` runs to learn what flows into this stage. `None` at the head of a
      /// pipeline, where nothing does.
      /// </remarks>
      Upstream: string option
      /// The arguments already written, without the one the cursor is in.
      Written: Tree.Argument list }

/// Which part of a command's arguments the word is.
[<RequireQualifiedAccess>]
type Slot =
    /// The parameter the word would bind to, counted the way the binder counts.
    | Parameter of Parameter
    /// The word starts with `-`: it names a flag.
    | Flag
    /// <summary>A `name=value` argument (decision 0017).</summary>
    /// <remarks>
    /// `Some name` when `name=` is written and the word is its value; `None` when a
    /// plain word stands past the positional parameters of a command that collects
    /// assignments, so it can only be the name of one.
    /// </remarks>
    | Assignment of name: string option
    /// <summary>More than the command takes.</summary>
    /// <remarks>Also the slot of every argument of a name that is not a command.</remarks>
    | Surplus

/// Where the word is, inside an argument handed to a `Predicate` parameter.
[<RequireQualifiedAccess>]
type Expression =
    /// A value starts here: `$row.`, `not`, a literal, a parenthesis.
    | Operand
    /// An operand has just been written, so a comparison operator comes next.
    | AfterOperand of left: Expr
    /// The right-hand side of a comparison.
    | ComparisonRight of op: string * left: Expr
    /// A whole comparison has been written: `and`, `or`, or the end of the argument.
    | AfterComparison

/// What kind of place the cursor is in.
[<RequireQualifiedAccess>]
type Place =
    /// Nothing typed at all.
    | Blank
    /// Where a command's name is written: the head of a line, after `|`, `else`, `try`
    /// or an opening parenthesis. `afterPipe` when a previous stage feeds it, and
    /// `upstream` the stages before it written out, so what they answer can be asked.
    | CommandName of afterPipe: bool * upstream: string option
    /// A `$` and the start of a name. `inPredicate` when it is inside an argument
    /// handed to a `Predicate` parameter, which is the only place `$row` exists.
    | Variable of inPredicate: bool
    /// <summary>After `$name.`, and possibly further members: `$row.`, `$x.a.`.</summary>
    /// <remarks>
    /// `path` is the members written between the variable and the one being typed.
    /// `stage` is the stage the word is in, when it is in one, so that `$row.` can ask
    /// what flows into it.
    /// </remarks>
    | Member of variable: string * path: string list * stage: Stage option
    | Argument of Stage * Slot
    | Predicate of Stage * Expression
    /// After `<`: a tag's type.
    | TagType
    /// Inside an open tag, after its type: an attribute's name.
    | TagAttribute of typeName: string
    /// The line could not be read. The lexical rules answer, as they did before.
    | Unknown

/// <summary>The word under the cursor.</summary>
/// <remarks>
/// `Start` to `End` is what a completion replaces, so that completing in the middle of
/// a line leaves the rest of it alone. When the word is quoted, `Start` is at the
/// opening quote and `Prefix` is what follows it.
/// </remarks>
type Word =
    { Start: int
      End: int
      /// What is written of the word before the cursor, sigils included: `$row.ki`,
      /// `-de`, `documents/no`. The opening quote of a quoted word is not included.
      Prefix: string
      Quoted: bool }

/// A line, a cursor, and what the cursor is in.
type Context =
    { Text: string
      Cursor: int
      Word: Word
      Place: Place }

[<RequireQualifiedAccess>]
module Context =

    /// <summary>What stands in for the word while the line is parsed.</summary>
    /// <remarks>
    /// An identifier, so it parses wherever a name, a word or a member can stand, and
    /// one no one will type.
    /// </remarks>
    let placeholder = "ZZcursorZZ"

    let private isPlaceholder (text: string) = text = placeholder

    // ---------------------------------------------------------------- The word

    let private isBoundary (c: char) =
        Char.IsWhiteSpace c || c = '|' || c = '(' || c = ','

    let private isIdentifierChar (c: char) = Char.IsLetterOrDigit c || c = '_'

    /// What the line has left open at its end, innermost last.
    type private Open =
        | Paren
        | TagOpen of typeName: string
        | VariableTagOpen

    type private Scan =
        { Stack: Open list
          /// Where an unclosed quote began, if one did.
          Quote: int option }

    /// <summary>Walks the text the way the grammar nests, without parsing it.</summary>
    /// <remarks>
    /// Enough to say what is open at the end: a quote, parentheses, tags. A string's
    /// body never contains a quote, so every quote opens or closes one.
    /// </remarks>
    let private scan (text: string) : Scan =
        let mutable stack: Open list = []
        let mutable quote: int option = None
        let mutable i = 0

        let at index =
            if index < text.Length then text[index] else '\000'

        while i < text.Length do
            let c = text[i]

            match quote with
            | Some _ ->
                if c = '"' then quote <- None
                i <- i + 1
            | None ->
                if c = '"' then
                    quote <- Some i
                    i <- i + 1
                elif c = '(' then
                    stack <- Paren :: stack
                    i <- i + 1
                elif c = ')' then
                    (match stack with
                     | Paren :: rest -> stack <- rest
                     | _ -> ())

                    i <- i + 1
                elif c = '<' && at (i + 1) = '$' then
                    stack <- VariableTagOpen :: stack
                    i <- i + 2
                elif c = '<' && isIdentifierChar (at (i + 1)) then
                    let mutable j = i + 1

                    while j < text.Length && isIdentifierChar text[j] do
                        j <- j + 1

                    // `<name|type`: the type is after the bar.
                    let first = text.Substring(i + 1, j - i - 1)

                    let typeName =
                        if at j = '|' then
                            let mutable k = j + 1

                            while k < text.Length && isIdentifierChar text[k] do
                                k <- k + 1

                            text.Substring(j + 1, k - j - 1)
                        else
                            first

                    stack <- TagOpen typeName :: stack
                    i <- j
                elif c = '/' && at (i + 1) = '>' then
                    (match stack with
                     | TagOpen _ :: rest -> stack <- rest
                     | _ -> ())

                    i <- i + 2
                elif c = '>' then
                    (match stack with
                     | TagOpen _ :: rest
                     | VariableTagOpen :: rest -> stack <- rest
                     | _ -> ())

                    i <- i + 1
                else
                    i <- i + 1

        { Stack = stack; Quote = quote }

    /// What closes whatever the text left open, innermost first.
    let private closers (text: string) =
        let scanned = scan text

        (if scanned.Quote.IsSome then "\"" else "")
        + (scanned.Stack
           |> List.map (function
               | Paren -> ")"
               | TagOpen _ -> "/>"
               | VariableTagOpen -> ">")
           |> String.concat "")

    /// <summary>Finds the word under the cursor.</summary>
    /// <remarks>
    /// It runs from the nearest whitespace, `|`, `(` or `,` before the cursor, or from
    /// an unclosed quote, to the end of the word after it.
    /// </remarks>
    let word (text: string) (cursor: int) : Word =
        let before = text.Substring(0, cursor)

        match (scan before).Quote with
        | Some quote ->
            let closing = text.IndexOf('"', cursor)
            let finish = if closing < 0 then text.Length else closing + 1

            { Start = quote
              End = finish
              Prefix = text.Substring(quote + 1, cursor - quote - 1)
              Quoted = true }
        | None ->
            let mutable start = cursor

            while start > 0 && not (isBoundary text[start - 1]) do
                start <- start - 1

            let mutable finish = cursor

            let stops (index: int) =
                let c = text[index]

                isBoundary c
                || c = ')'
                || c = '"'
                || c = '>'
                || c = '}'
                || (c = '/' && index + 1 < text.Length && (text[index + 1] = '>' || text[index + 1] = '}'))

            while finish < text.Length && not (stops finish) do
                finish <- finish + 1

            { Start = start
              End = finish
              Prefix = text.Substring(start, cursor - start)
              Quoted = false }

    // ---------------------------------------------------------------- Its form

    /// What the word says about itself before the line is parsed.
    type private Form =
        | Plain
        | Quoted
        /// `$name`, or `$name.a.b` with `members` the ones before the stop being typed.
        | Dollar of name: string * members: string list * hasStop: bool
        | DashFlag
        | Assigned of name: string
        | TagTypeForm
        | VariableTagForm
        | InTag of typeName: string
        | InTagValue

    let private formOf (text: string) (word: Word) : Form =
        let prefix = word.Prefix
        let openTag =
            match (scan (text.Substring(0, word.Start))).Stack with
            | TagOpen typeName :: _ -> Some typeName
            | _ -> None

        if word.Quoted then
            Quoted
        elif prefix.StartsWith "<$" then
            VariableTagForm
        elif prefix.StartsWith "<" then
            TagTypeForm
        elif openTag.IsSome then
            if prefix.Contains "=" then InTagValue else InTag openTag.Value
        elif prefix.StartsWith "$" then
            let parts = prefix.Substring(1).Split('.')

            if parts.Length = 1 then
                Dollar(parts[0], [], false)
            else
                Dollar(parts[0], parts[1 .. parts.Length - 2] |> List.ofArray, true)
        elif prefix.StartsWith "-" && not (prefix.Length > 1 && Char.IsDigit prefix[1]) then
            DashFlag
        elif prefix.IndexOf '=' > 0 && prefix.Substring(0, prefix.IndexOf '=') |> Seq.forall isIdentifierChar then
            Assigned(prefix.Substring(0, prefix.IndexOf '='))
        else
            Plain

    /// What the word becomes in the line that is parsed.
    let private replacement (form: Form) (word: Word) =
        match form with
        | Plain -> placeholder
        | Quoted -> "\"" + placeholder + "\""
        | Dollar(_, _, false) -> "$" + placeholder
        | Dollar(_, _, true) -> word.Prefix.Substring(0, word.Prefix.LastIndexOf '.' + 1) + placeholder
        | DashFlag -> "-" + placeholder
        | Assigned name -> name + "=" + placeholder
        | TagTypeForm -> "<" + placeholder + "/>"
        | VariableTagForm -> "<$" + placeholder + ">"
        | InTag _ -> placeholder + "=" + placeholder
        | InTagValue -> word.Prefix.Substring(0, word.Prefix.IndexOf '=' + 1) + placeholder

    // ---------------------------------------------------------------- The tree

    let rec private valueHas (node: Tree.Value) : bool =
        match node with
        | null -> false
        | :? Tree.Identifier as identifier -> isPlaceholder identifier.Name
        | :? Tree.StringConstant as text -> isPlaceholder text.Value
        | :? Tree.VariableReference as reference ->
            isPlaceholder reference.Name.Name
            || reference.Members |> Seq.exists (fun m -> isPlaceholder m.Name)
        | :? Tree.ComparisonExpression as comparison -> valueHas comparison.Left || valueHas comparison.Right
        | :? Tree.BooleanExpression as combination -> valueHas combination.Left || valueHas combination.Right
        | :? Tree.NotExpression as negation -> valueHas negation.Operand
        | :? Tree.NestedPipeline as nested -> pipelineHas nested.Pipeline
        | :? Tree.TagValue as tag -> tagHas tag.Tag
        | _ -> false

    and private tagHas (tag: Tree.Tag) : bool =
        let attributesHave (attributes: Tree.TagAttributeList) =
            not (isNull (box attributes))
            && attributes.Attributes
               |> Seq.exists (fun a -> isPlaceholder a.Name.Name || valueHas a.Value)

        let childrenHave (children: Tree.TagList) =
            not (isNull (box children)) && children.Tags |> Seq.exists tagHas

        match tag with
        | :? Tree.ObjectInstance as instance ->
            isPlaceholder instance.ObjectType.Value
            || attributesHave instance.Attributes
            || childrenHave instance.Children
        | :? Tree.ComponentInstance as instance ->
            isPlaceholder instance.ComponentType.Value
            || attributesHave instance.Attributes
            || childrenHave instance.Children
        | :? Tree.VariableTag as reference -> isPlaceholder reference.Name.Name
        | _ -> false

    and private argumentHas (argument: Tree.Argument) : bool =
        match argument with
        | :? Tree.Flag as flag -> isPlaceholder flag.Name
        | :? Tree.Assignment as assignment -> isPlaceholder assignment.Name.Name || valueHas assignment.Value
        | :? Tree.RequiredArgument as required -> valueHas required.Value
        | :? Tree.ArgumentValue as value -> valueHas value.Value
        | :? Tree.NamedArgument as named -> valueHas named.Value
        | _ -> false

    and private argumentsHave (arguments: Tree.Arguments) =
        not (isNull (box arguments)) && arguments.Arguments |> Seq.exists argumentHas

    and private stageHas (stage: Tree.Expression) : bool =
        let own =
            stage.Expression.Match(
                (fun (f: Tree.Function) -> isPlaceholder f.Id.Name || argumentsHave f.Arguments),
                (fun (c: Tree.Cli) -> isPlaceholder c.Name.Name || argumentsHave c.Arguments),
                (fun (tag: Tree.InstanceTag) -> tagHas tag),
                (fun (nested: Tree.NestedPipeline) -> pipelineHas nested.Pipeline),
                (fun (reference: Tree.VariableReference) -> valueHas reference))

        own || valueHas stage.Default

    and private pipelineHas (pipeline: Tree.Pipeline) =
        pipeline.OrderedCommands |> Seq.exists stageHas

    /// A nested pipeline holding the placeholder, reached without passing through a tag.
    let rec private nestedWithin (node: Tree.Value) : Tree.NestedPipeline option =
        match node with
        | :? Tree.NestedPipeline as nested when pipelineHas nested.Pipeline -> Some nested
        | :? Tree.ComparisonExpression as comparison ->
            nestedWithin comparison.Left |> Option.orElse (nestedWithin comparison.Right)
        | :? Tree.BooleanExpression as combination ->
            nestedWithin combination.Left |> Option.orElse (nestedWithin combination.Right)
        | :? Tree.NotExpression as negation -> nestedWithin negation.Operand
        | _ -> None

    let private plainValue (argument: Tree.Argument) =
        match argument with
        | :? Tree.RequiredArgument as required -> Some required.Value
        | :? Tree.ArgumentValue as value -> Some value.Value
        | _ -> None

    let private writtenValue (argument: Tree.Argument) =
        match plainValue argument with
        | Some value -> Some value
        | None ->
            match argument with
            | :? Tree.NamedArgument as named -> Some named.Value
            | :? Tree.Assignment as assignment -> Some assignment.Value
            | _ -> None

    let private toExpr (node: Tree.Value) =
        match Expr.ofNode node with
        | Ok expr -> expr
        | Error _ -> Expr.Const Value.Empty

    /// Where in a predicate the placeholder is, given the argument that holds it.
    let rec private expressionOf (node: Tree.Value) : Expression =
        match node with
        | :? Tree.ComparisonExpression as comparison ->
            if valueHas comparison.Right then
                Expression.ComparisonRight(comparison.Operator.Name, toExpr comparison.Left)
            else
                expressionOf comparison.Left
        | :? Tree.BooleanExpression as combination ->
            if valueHas combination.Right then expressionOf combination.Right else expressionOf combination.Left
        | :? Tree.NotExpression as negation -> expressionOf negation.Operand
        | _ -> Expression.Operand

    /// What comes after a predicate argument that is already written.
    let rec private after (node: Tree.Value) : Expression =
        match node with
        | :? Tree.BooleanExpression as combination -> after combination.Right
        | :? Tree.NotExpression as negation -> after negation.Operand
        | :? Tree.ComparisonExpression -> Expression.AfterComparison
        | other -> Expression.AfterOperand(toExpr other)

    /// How one written argument binds.
    type private Binding =
        | ToParameter of Parameter
        | AsFlag
        | AsAssignment of string option
        | Unbound

    let private findNamed (spec: CommandSpec) (name: string) =
        spec.Parameters
        |> List.tryFind (fun parameter ->
            String.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase)
            || (match parameter.Flag with
                | Some flag -> String.Equals(flag, name, StringComparison.OrdinalIgnoreCase)
                | None -> false))

    /// <summary>How every written argument binds, by its index.</summary>
    /// <remarks>
    /// Steps 1 and 2 of `Binder.bindWith`, without evaluating anything: named
    /// arguments, flags and assignments first, then each declared parameter takes the
    /// next positional argument, and a `Rest` parameter takes them all.
    /// </remarks>
    let private bindings (spec: CommandSpec) (written: Tree.Argument[]) : Map<int, Binding> =
        let mutable result = Map.empty
        let mutable named = Set.empty
        let positional = ResizeArray<int>()
        let mutable index = 0

        while index < written.Length do
            match written[index] with
            | :? Tree.NamedArgument as argument ->
                let name = argument.Name.Match((fun flag -> flag.Name), (fun identifier -> identifier.Name))

                match findNamed spec name with
                | Some parameter ->
                    result <- Map.add index (ToParameter parameter) result
                    named <- Set.add parameter.Name named
                | None -> result <- Map.add index Unbound result

                index <- index + 1
            | :? Tree.Flag as flag ->
                result <- Map.add index AsFlag result

                match findNamed spec flag.Name with
                | Some parameter ->
                    named <- Set.add parameter.Name named

                    // `-flag value` takes the next plain argument, as the binder does.
                    if index + 1 < written.Length && (plainValue written[index + 1]).IsSome then
                        result <- Map.add (index + 1) (ToParameter parameter) result
                        index <- index + 2
                    else
                        index <- index + 1
                | None -> index <- index + 1
            | :? Tree.Assignment as assignment ->
                result <- Map.add index (AsAssignment(Some assignment.Name.Name)) result
                index <- index + 1
            | _ ->
                positional.Add index
                index <- index + 1

        let mutable queue = List.ofSeq positional

        for parameter in CommandSpec.positional spec do
            if not (Set.contains parameter.Name named) then
                match parameter.Kind, queue with
                | ParamKind.Rest, _ ->
                    for taken in queue do
                        result <- Map.add taken (ToParameter parameter) result

                    queue <- []
                | _, taken :: rest ->
                    result <- Map.add taken (ToParameter parameter) result
                    queue <- rest
                | _, [] -> ()

        // What is left over is more than the command takes, unless it collects
        // assignments, when a plain word there can only be the name of one.
        let leftover =
            if (CommandSpec.assignmentParameter spec).IsSome then AsAssignment None else Unbound

        for taken in queue do
            result <- Map.add taken leftover result

        result

    /// <summary>The stages before one, written out as a line.</summary>
    let private upstreamOf (pipeline: Tree.Pipeline) (index: int) =
        if index = 0 then
            None
        else
            let before = Tree.Pipeline()

            pipeline.OrderedCommands |> Seq.take index |> Seq.iter before.OrderedCommands.Add

            let visitor = Isagri.Reporting.Quid.RequestFilters.SemanticTree.SerialisationVisitor()
            before.Accept visitor
            Some(visitor.GetResult().Trim())

    let rec private inPipeline (specs: CommandSpec list) (pipeline: Tree.Pipeline) : Place option =
        pipeline.OrderedCommands
        |> Seq.indexed
        |> Seq.tryFind (fun (_, stage) -> stageHas stage)
        |> Option.map (fun (index, stage) -> inStage specs pipeline index stage)

    and private inStage specs pipeline index (stage: Tree.Expression) : Place =
        if valueHas stage.Default then
            match nestedWithin stage.Default with
            | Some nested -> inPipeline specs nested.Pipeline |> Option.defaultValue Place.Unknown
            | None -> Place.Unknown
        else
            stage.Expression.Match(
                (fun (f: Tree.Function) ->
                    if isPlaceholder f.Id.Name then
                        Place.CommandName(index > 0, upstreamOf pipeline index)
                    else
                        inArguments specs pipeline index f.Id.Name f.Arguments),
                (fun (c: Tree.Cli) ->
                    if isPlaceholder c.Name.Name then
                        Place.CommandName(index > 0, upstreamOf pipeline index)
                    else
                        inArguments specs pipeline index c.Name.Name c.Arguments),
                // A tag standing alone: its type and attributes are read from the word.
                (fun (_: Tree.InstanceTag) -> Place.Unknown),
                (fun (nested: Tree.NestedPipeline) ->
                    inPipeline specs nested.Pipeline |> Option.defaultValue Place.Unknown),
                // A variable standing as a stage (decision 0032): the `$` word is read
                // from its form, so the tree has nothing to add.
                (fun (_: Tree.VariableReference) -> Place.Unknown))

    and private inArguments specs pipeline index (name: string) (arguments: Tree.Arguments) : Place =
        let written =
            if isNull (box arguments) then [||] else Array.ofSeq arguments.Arguments

        let at = written |> Array.findIndex argumentHas
        let holding = written[at]

        let nested = writtenValue holding |> Option.bind nestedWithin

        match nested with
        | Some nested -> inPipeline specs nested.Pipeline |> Option.defaultValue Place.Unknown
        | None ->
            let spec =
                specs |> List.tryFind (fun s -> String.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))

            let stage =
                { Spec = spec
                  Name = name
                  Index = index
                  Upstream = upstreamOf pipeline index
                  Written = written |> Array.removeAt at |> List.ofArray }

            match spec with
            | None -> Place.Argument(stage, (if holding :? Tree.Flag then Slot.Flag else Slot.Surplus))
            | Some spec ->
                let bound = bindings spec written

                let bindingAt i =
                    Map.tryFind i bound |> Option.defaultValue Unbound

                // A plain word straight after a predicate's argument continues the
                // predicate: `where $row.kind |` is waiting for an operator, not for
                // the table.
                let continuing =
                    match holding with
                    | :? Tree.ArgumentValue as value when (value.Value :? Tree.Identifier) && at > 0 ->
                        match bindingAt (at - 1), plainValue written[at - 1] with
                        | ToParameter parameter, Some previous when parameter.Kind = ParamKind.Predicate ->
                            Some(Place.Predicate(stage, after previous))
                        | _ -> None
                    | _ -> None

                match continuing with
                | Some place -> place
                | None ->
                    match bindingAt at with
                    | AsFlag -> Place.Argument(stage, Slot.Flag)
                    | AsAssignment name -> Place.Argument(stage, Slot.Assignment name)
                    | Unbound -> Place.Argument(stage, Slot.Surplus)
                    | ToParameter parameter when parameter.Kind = ParamKind.Predicate ->
                        match writtenValue holding with
                        | Some value -> Place.Predicate(stage, expressionOf value)
                        | None -> Place.Predicate(stage, Expression.Operand)
                    | ToParameter parameter -> Place.Argument(stage, Slot.Parameter parameter)

    let private parser = lazy (CommandLineParser())

    let private structural (specs: CommandSpec list) (line: string) : Place option =
        let parsed = parser.Value.Parse<Tree.Node> line

        if not parsed.IsT0 then
            None
        else
            match parsed.AsT0 with
            | :? Tree.Pipeline as pipeline -> inPipeline specs pipeline
            | :? Tree.RecoveryLine as recovery -> recovery.Pipelines |> Seq.tryPick (inPipeline specs)
            | _ -> None

    let private stageOf (place: Place) =
        match place with
        | Place.Argument(stage, _)
        | Place.Predicate(stage, _) -> Some stage
        | _ -> None

    /// <summary>Reads where the cursor is.</summary>
    /// <remarks>
    /// The cursor is clamped to the text. Every line gives an answer; a line that
    /// cannot be read gives `Unknown`, and the lexical rules answer it as before.
    /// </remarks>
    let analyse (specs: CommandSpec list) (text: string) (cursor: int) : Context =
        let text = if isNull text then "" else text
        let cursor = max 0 (min cursor text.Length)
        let word = word text cursor
        let form = formOf text word

        let place =
            if String.IsNullOrWhiteSpace text then
                Place.Blank
            else
                let before = text.Substring(0, word.Start) + replacement form word
                let tail = text.Substring word.End

                // The whole line first, then the line cut at the word, in case what
                // follows the cursor is what does not parse.
                let found =
                    [ before + tail; before ]
                    |> List.tryPick (fun line -> structural specs (line + closers line))

                match form with
                | TagTypeForm -> Place.TagType
                | InTag typeName -> Place.TagAttribute typeName
                | InTagValue -> Place.Unknown
                | VariableTagForm -> Place.Variable false
                | Dollar(name, _, false) when not word.Quoted ->
                    let inPredicate =
                        match found with
                        | Some(Place.Predicate _) -> true
                        | _ -> false

                    // `$` at the head of a stage is a value stage (decision 0032), and
                    // still a variable being named.
                    ignore name
                    Place.Variable inPredicate
                | Dollar(name, members, true) -> Place.Member(name, members, found |> Option.bind stageOf)
                | _ -> found |> Option.defaultValue Place.Unknown

        { Text = text
          Cursor = cursor
          Word = word
          Place = place }

    // ---------------------------------------------------------------- For tests

    /// <summary>A place in a line, as text: `Argument(sort, column)`.</summary>
    /// <remarks>
    /// So a test can say what it expects in one short string, and does not break when a
    /// stream adds a field to `Parameter` or a record that a place carries.
    /// </remarks>
    let describe (place: Place) : string =
        let stageText (stage: Stage) = stage.Name

        let expression =
            function
            | Expression.Operand -> "Operand"
            | Expression.AfterOperand left -> sprintf "AfterOperand %s" (Value.exprText left)
            | Expression.ComparisonRight(op, left) -> sprintf "ComparisonRight %s %s" (Value.exprText left) op
            | Expression.AfterComparison -> "AfterComparison"

        match place with
        | Place.Blank -> "Blank"
        | Place.CommandName(afterPipe, _) -> if afterPipe then "CommandName afterPipe" else "CommandName"
        | Place.Variable inPredicate -> if inPredicate then "Variable inPredicate" else "Variable"
        | Place.Member(variable, path, stage) ->
            let path = path |> List.map (fun m -> "." + m) |> String.concat ""

            match stage with
            | Some stage -> sprintf "Member $%s%s in %s" variable path (stageText stage)
            | None -> sprintf "Member $%s%s" variable path
        | Place.Argument(stage, slot) ->
            let slot =
                match slot with
                | Slot.Parameter parameter -> parameter.Name
                | Slot.Flag -> "flag"
                | Slot.Assignment(Some name) -> name + "="
                | Slot.Assignment None -> "assignment"
                | Slot.Surplus -> "surplus"

            sprintf "Argument(%s, %s)" (stageText stage) slot
        | Place.Predicate(stage, e) -> sprintf "Predicate(%s, %s)" (stageText stage) (expression e)
        | Place.TagType -> "TagType"
        | Place.TagAttribute typeName -> sprintf "TagAttribute %s" typeName
        | Place.Unknown -> "Unknown"
