/// An argument, by what its parameter takes; and the signature hint (stream D).
namespace CommandLineReimagined.Core

open System

[<RequireQualifiedAccess>]
module ArgumentCompletion =

    let private startsWith (prefix: string) (candidate: string) =
        candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)

    let private ordinal (a: string) (b: string) = String.CompareOrdinal(a, b)

    /// A column's type, in the words `columns` answers with.
    let private typeName (columnType: ColumnType) =
        match columnType with
        | TextCol -> "text"
        | NumberCol -> "number"
        | BooleanCol -> "boolean"
        | FileCol -> "file"
        | ObjectCol -> "object"
        | MixedCol -> "mixed"

    /// <summary>`else`, where an argument is written and two letters of it are there.</summary>
    /// <remarks>
    /// The lexical rule, kept: `e` alone is far more often the start of a file name, and
    /// a quoted word is text, never the keyword.
    /// </remarks>
    let private keywords (request: Request) =
        let word = request.Context.Word

        if word.Quoted || word.Prefix.Length < 2 then
            []
        else
            Lexical.lineKeywords
            |> List.filter (startsWith word.Prefix)
            |> List.map (Request.item request "keyword")

    /// <summary>The variables in scope, each with what it holds.</summary>
    /// <remarks>
    /// `sigil` is `$` where the word is a value and "" where it is a variable's name, as
    /// `set` takes it. The prefix is matched with or without its `$`.
    /// </remarks>
    let private variables (request: Request) (sigil: string) =
        let prefix = request.Context.Word.Prefix.TrimStart '$'

        request.Projection.Variables
        |> Map.toList
        |> List.filter (fun (name, _) -> startsWith prefix name)
        |> List.sortWith (fun (a, _) (b, _) -> ordinal a b)
        |> List.map (fun (name, value) ->
            Request.item request "variable" (sigil + name) |> Request.withDetail (Summary.ofValue value))

    let private commands (request: Request) =
        request.Specs
        |> List.filter (fun spec -> startsWith request.Context.Word.Prefix spec.Name)
        |> List.map (fun spec -> Request.item request "command" spec.Name |> Request.withDetail spec.Description)

    /// The text of an argument written as a plain word or a string, which is how a
    /// column or a path is written.
    let private plainText (argument: Tree.Argument) =
        let text (value: Tree.Value) =
            match value with
            | :? Tree.Identifier as identifier -> Some identifier.Name
            | :? Tree.StringConstant as constant -> Some constant.Value
            | _ -> None

        match argument with
        | :? Tree.RequiredArgument as required -> text required.Value
        | :? Tree.ArgumentValue as value -> text value.Value
        | _ -> None

    /// <summary>The columns of what flows into the stage.</summary>
    /// <remarks>
    /// `request.Shapes` answers the current folder's listing until stream F runs the
    /// upstream. A `Rest` of columns leaves out the ones already written, since
    /// `select name name` asks for nothing more.
    /// </remarks>
    let private columns (request: Request) (stage: Stage) (parameter: Parameter) =
        async {
            let! shape = request.Shapes stage

            let written =
                if parameter.Kind = ParamKind.Rest then
                    stage.Written |> List.choose plainText |> Set.ofList
                else
                    Set.empty

            return
                shape.Columns
                |> List.filter (fun (name, _) -> startsWith request.Context.Word.Prefix name && not (Set.contains name written))
                |> List.map (fun (name, columnType) ->
                    Request.item request "column" name |> Request.withDetail (typeName columnType))
        }

    /// What a parameter's argument could be, by what it takes.
    let private byTakes (request: Request) (stage: Stage) (parameter: Parameter) : Async<Completion list> =
        let now items = async.Return items

        match parameter.Takes with
        | Takes.Anything
        | Takes.Path -> now (PathCompletion.suggest request false)
        | Takes.Place -> now (PathCompletion.suggest request true)
        | Takes.Column -> columns request stage parameter
        | Takes.Switch(on, off) ->
            // The description is written for the word that turns it on.
            let item word =
                let completion = Request.item request "keyword" word
                if word = on then Request.withDetail parameter.Description completion else completion

            (on :: Option.toList off)
            |> List.filter (startsWith request.Context.Word.Prefix)
            |> List.map item
            |> now
        | Takes.VariableName -> now (variables request "")
        | Takes.CommandName -> now (commands request)
        | Takes.Value -> now (variables request "$")
        // Nothing to pick: the signature says what is wanted.
        | Takes.Count
        | Takes.Number
        | Takes.NewName
        | Takes.Text
        | Takes.Url -> now []

    /// The names of the parameters a written flag or `name:` argument already binds.
    let private named (stage: Stage) =
        stage.Written
        |> List.choose (fun argument ->
            match argument with
            | :? Tree.Flag as flag -> Some flag.Name
            | :? Tree.NamedArgument as argument ->
                Some(argument.Name.Match((fun flag -> flag.Name), (fun identifier -> identifier.Name)))
            | _ -> None)
        |> List.map (fun name -> name.ToLowerInvariant())
        |> Set.ofList

    let private flagOf (parameter: Parameter) = parameter.Flag |> Option.defaultValue parameter.Name

    /// <summary>After `-`: the command's flags.</summary>
    /// <remarks>
    /// Switches and optional parameters, which are what a flag is written for; not the
    /// one that takes the pipe, a collector, or one already written.
    /// </remarks>
    let private flags (request: Request) (stage: Stage) =
        match stage.Spec with
        | None -> []
        | Some spec ->
            let written = named stage

            let isSwitch (parameter: Parameter) =
                match parameter.Takes with
                | Takes.Switch _ -> true
                | _ -> false

            spec.Parameters
            |> List.filter (fun parameter ->
                parameter.Kind = ParamKind.Single
                && not parameter.AcceptsPipe
                && (parameter.Optional || isSwitch parameter)
                && not (Set.contains (parameter.Name.ToLowerInvariant()) written)
                && not (Set.contains ((flagOf parameter).ToLowerInvariant()) written))
            |> List.map (fun parameter -> parameter, "-" + flagOf parameter)
            |> List.filter (fun (_, text) -> startsWith request.Context.Word.Prefix text)
            |> List.map (fun (parameter, text) -> Request.item request "flag" text |> Request.withDetail parameter.Description)

    /// <summary>After `attr <file> `: that record's attributes, as `name=`.</summary>
    /// <remarks>
    /// The record is the one the first plain word names. The attributes offered are the
    /// ones a user may write (decision 0013): its own, then `name` and `kind`; not the
    /// ones the terminal keeps, nor one already assigned on this line.
    /// </remarks>
    let private attributes (request: Request) (stage: Stage) =
        let assigned =
            stage.Written
            |> List.choose (fun argument ->
                match argument with
                | :? Tree.Assignment as assignment -> Some assignment.Name.Name
                | _ -> None)
            |> Set.ofList

        let record =
            stage.Written
            |> List.tryPick plainText
            |> Option.bind (fun path ->
                match Files.resolve request.Projection request.Projection.Location path with
                | Ok record -> Some record
                | Error _ -> None)

        match record with
        | None -> []
        | Some record ->
            let own =
                record.Attributes
                |> Map.toList
                |> List.filter (fun (name, _) -> not (List.contains name Attributes.owned))
                |> List.sortWith (fun (a, _) (b, _) -> ordinal a b)

            let settable =
                [ Attributes.name; Attributes.kind ]
                |> List.choose (fun name -> Map.tryFind name record.Attributes |> Option.map (fun value -> name, value))

            own @ settable
            |> List.filter (fun (name, _) -> not (Set.contains name assigned))
            |> List.filter (fun (name, _) -> startsWith request.Context.Word.Prefix name)
            |> List.map (fun (name, value) ->
                Request.item request "member" (name + "=") |> Request.withDetail (Summary.ofValue value))

    /// <summary>What an argument could be.</summary>
    /// <remarks>
    /// By the slot, and for a parameter by what it `Takes`: columns for a column, the
    /// switch's words for a switch, nothing for a count. A word past what the command
    /// takes is offered files, as it always was. `else` is offered wherever an
    /// argument is written, once two letters of it are there.
    /// </remarks>
    let suggest (request: Request) (stage: Stage) (slot: Slot) : Async<Completion list> =
        async {
            match slot with
            | Slot.Flag -> return flags request stage
            // The value of `name=`: anything at all.
            | Slot.Assignment(Some _) -> return []
            | Slot.Assignment None -> return keywords request @ attributes request stage
            | Slot.Surplus -> return keywords request @ PathCompletion.suggest request false
            | Slot.Parameter parameter ->
                let! items = byTakes request stage parameter
                return keywords request @ items
        }

    /// <summary>The signature of the command the cursor is in, if it is in one.</summary>
    /// <remarks>
    /// For every `Argument` and `Predicate` place of a known command, with `Active` the
    /// parameter the word would bind to: the slot's parameter, the collector for an
    /// assignment, the flag's parameter once it is written out, and the predicate
    /// parameter inside a predicate. A word past what the command takes binds to
    /// nothing, so nothing is marked.
    /// </remarks>
    let signature (request: Request) : Signature option =
        let build (stage: Stage) (active: CommandSpec -> Parameter option) =
            stage.Spec
            |> Option.map (fun spec ->
                { Command = spec.Name
                  Description = spec.Description
                  Parameters = spec.Parameters |> List.map (fun p -> p.Name, p.Optional, p.Description)
                  Active =
                    active spec
                    |> Option.bind (fun parameter -> spec.Parameters |> List.tryFindIndex (fun p -> p.Name = parameter.Name)) })

        match request.Context.Place with
        | Place.Argument(stage, slot) ->
            build stage (fun spec ->
                match slot with
                | Slot.Parameter parameter -> Some parameter
                | Slot.Assignment _ -> CommandSpec.assignmentParameter spec
                | Slot.Flag ->
                    let written = request.Context.Word.Prefix.TrimStart '-'

                    spec.Parameters
                    |> List.tryFind (fun p -> String.Equals(flagOf p, written, StringComparison.OrdinalIgnoreCase))
                | Slot.Surplus -> None)
        | Place.Predicate(stage, _) ->
            build stage (fun spec -> spec.Parameters |> List.tryFind (fun p -> p.Kind = ParamKind.Predicate))
        | _ -> None
