/// What a tapped token is (stream G).
///
/// The page used to answer a tap with the token's grammar role alone: `variable —
/// $files`. That is what the parser knows, and it is the least interesting thing about
/// the token. This reads the line the way completion does (`Context`), and says what
/// the token means where it stands: what a variable holds, what a command takes, what
/// type a column is, what an operator compares.
namespace CommandLineReimagined.Core

open System

/// What the page shows for a token.
type Hover =
    { /// `variable`, `command`, `member`, `operator` …, as the page colours it.
      Kind: string
      /// The token as written.
      Text: string
      /// The line about it: a variable's summary, a column's type, what an operator compares.
      Detail: string option
      /// A command's signature, for a command name.
      Signature: Signature option }

[<RequireQualifiedAccess>]
module Hover =

    /// What each word operator asks, as the end of "true when the left …".
    let private meanings =
        [ "eq", "is equal to"
          "ne", "is not equal to"
          "gt", "is greater than"
          "ge", "is greater than or equal to"
          "lt", "is less than"
          "le", "is less than or equal to"
          "like", "matches the pattern (* for anything)"
          "has", "contains" ]
        |> Map.ofList

    let private equalsIgnoringCase (a: string) (b: string) =
        String.Equals(a, b, StringComparison.OrdinalIgnoreCase)

    /// <summary>A command's parameters, in the shape the signature hint draws.</summary>
    /// <remarks>
    /// Built from the command's own declaration, so a tap on `sort` and the hint while
    /// typing its arguments say the same thing.
    /// </remarks>
    let signatureOf (spec: CommandSpec) (active: Parameter option) : Signature =
        { Command = spec.Name
          Description = spec.Description
          Parameters = spec.Parameters |> List.map (fun p -> p.Name, p.Optional, p.Description)
          Active =
            active
            |> Option.bind (fun wanted -> spec.Parameters |> List.tryFindIndex (fun p -> p.Name = wanted.Name)) }

    let private columnTypeName (columnType: ColumnType) =
        match columnType with
        | TextCol -> "text"
        | NumberCol -> "number"
        | BooleanCol -> "boolean"
        | FileCol -> "file"
        | ObjectCol -> "object"
        | MixedCol -> "mixed"

    let private columnDetail (columnType: ColumnType) = "column · " + columnTypeName columnType

    /// The word the cursor is at the end of, as it is written on the line.
    let private written (context: Context) =
        let word = context.Word
        context.Text.Substring(word.Start, word.End - word.Start)

    /// The next word after the token, for the right-hand side of a comparison.
    let private following (context: Context) =
        let rest = context.Text.Substring(context.Word.End).TrimStart()
        let stop = rest.IndexOfAny [| ' '; '\t'; '|'; ')' |]
        let next = if stop < 0 then rest else rest.Substring(0, stop)
        if next.Length = 0 then None else Some next

    /// <summary>A variable, by what it holds.</summary>
    /// <remarks>
    /// `$row` is never in the projection: it exists only while a predicate is testing a
    /// row (decision 0008), so it is described by where it stands instead.
    /// </remarks>
    let private variable (request: Request) (text: string) (name: string) (inPredicate: bool) =
        let detail =
            if name = "row" then
                if inPredicate then
                    "the row being tested"
                else
                    "the row a predicate is testing, only inside where, find, in and save-view"
            else
                match Map.tryFind name request.Projection.Variables with
                | Some value -> Summary.ofValue value
                | None -> "not set"

        { Kind = "variable"
          Text = text
          Detail = Some detail
          Signature = None }

    /// <summary>A member, by what reading it gives.</summary>
    /// <remarks>
    /// `$row.` reads a column of what flows into the stage, so its type comes from
    /// `Shape`. Any other variable is read: a table's column by its type, a tag's, a
    /// file's or a fault's member by its value.
    /// </remarks>
    let private memberOf (request: Request) (text: string) (name: string) (path: string list) (stage: Stage option) =
        async {
            let wanted = text.Substring(text.LastIndexOf '.' + 1)

            let columnIn (columns: (string * ColumnType) list) =
                columns
                |> List.tryFind (fun (column, _) -> equalsIgnoringCase column wanted)
                |> Option.map (snd >> columnDetail)

            let! detail =
                if name = "row" then
                    match stage, path with
                    | Some stage, [] ->
                        async {
                            let! shape = request.Shapes stage
                            return columnIn shape.Columns
                        }
                    | _ -> async.Return None
                else
                    match Map.tryFind name request.Projection.Variables with
                    | None -> async.Return None
                    | Some held ->
                        let reached = path |> List.fold (fun value m -> Expr.readMember m value) held

                        match reached with
                        | Value.Table table ->
                            async.Return(columnIn (table.Columns |> List.map (fun c -> c.Name, c.Type)))
                        | other ->
                            match Expr.readMember wanted other with
                            | Value.None -> async.Return None
                            | value -> async.Return(Some(Summary.ofValue value))

            return
                { Kind = "member"
                  Text = text
                  Detail = detail
                  Signature = None }
        }

    /// <summary>An operator, by what it compares.</summary>
    /// <remarks>
    /// The left side is what the parse says came before; the right is the next word on
    /// the line, as written.
    /// </remarks>
    let private operator (text: string) (context: Context) (left: Expr option) =
        let op = text.ToLowerInvariant()
        let right = following context

        let detail =
            match op with
            | "and" -> "true when both sides are true"
            | "or" -> "true when either side is true"
            | "not" -> "true when what follows is false"
            | _ ->
                let meaning = Map.find op meanings

                match left, right with
                | Some left, Some right -> sprintf "true when %s %s %s" (Value.exprText left) meaning right
                | Some left, None -> sprintf "true when %s %s the value after it" (Value.exprText left) meaning
                | None, Some right -> sprintf "true when the value before it %s %s" meaning right
                | None, None -> sprintf "true when the value before it %s the value after it" meaning

        { Kind = "operator"
          Text = text
          Detail = Some detail
          Signature = None }

    let private isOperator (text: string) =
        let op = text.ToLowerInvariant()
        Map.containsKey op meanings || op = "and" || op = "or" || op = "not"

    let private command (request: Request) (text: string) =
        match request.Specs |> List.tryFind (fun spec -> equalsIgnoringCase spec.Name text) with
        | Some spec ->
            Some
                { Kind = "command"
                  Text = text
                  Detail = Some spec.Description
                  Signature = Some(signatureOf spec None) }
        | None -> None

    /// An argument, by the parameter it binds to.
    let private argument (text: string) (kind: string) (stage: Stage) (parameter: Parameter option) =
        match stage.Spec with
        | None -> None
        | Some spec ->
            Some
                { Kind = kind
                  Text = text
                  Detail = parameter |> Option.map (fun p -> p.Name + " · " + p.Description)
                  Signature = Some(signatureOf spec parameter) }

    let private findFlag (spec: CommandSpec) (name: string) =
        spec.Parameters
        |> List.tryFind (fun p ->
            equalsIgnoringCase p.Name name
            || (match p.Flag with
                | Some flag -> equalsIgnoringCase flag name
                | None -> false))

    let private predicateParameter (stage: Stage) =
        stage.Spec |> Option.bind (fun spec -> spec.Parameters |> List.tryFind (fun p -> p.Kind = ParamKind.Predicate))

    /// <summary>What the token at the request's cursor is.</summary>
    /// <remarks>
    /// The page asks with the cursor at the end of the token, so the word under it is
    /// the whole token, sigils included. `None` for a token there is nothing to say
    /// about, and the page shows its grammar role instead.
    /// </remarks>
    let describe (request: Request) : Async<Hover option> =
        async {
            let context = request.Context
            let text = written context

            if text.Length = 0 then
                return None
            else
                match context.Place with
                | Place.Variable inPredicate ->
                    // The variable alone, not the members after it: `$row` in `$row.kind`
                    // is tapped as a token of its own.
                    // The page may tap the `$` alone, which the tokens keep apart from
                    // the name, and then the name is after the cursor.
                    let reference =
                        if context.Word.Prefix.TrimStart('<').TrimStart('$').Length = 0 then text
                        else context.Word.Prefix

                    let name = reference.TrimStart('<').TrimStart('$').Split('.')[0]
                    return Some(variable request ("$" + name) name inPredicate)
                | Place.Member(name, path, stage) ->
                    // Up to the cursor, which is the end of the member tapped: the
                    // members after it are tokens of their own.
                    let! hover = memberOf request context.Word.Prefix name path stage
                    return Some hover
                | Place.CommandName _ -> return command request text
                | Place.Predicate(_, expression) when isOperator text ->
                    let left =
                        match expression with
                        | Expression.AfterOperand left -> Some left
                        | Expression.ComparisonRight(_, left) -> Some left
                        | _ -> None

                    return Some(operator text context left)
                | Place.Predicate(stage, _) -> return argument text "argument" stage (predicateParameter stage)
                | Place.Argument(stage, Slot.Flag) ->
                    let parameter =
                        stage.Spec |> Option.bind (fun spec -> findFlag spec (text.TrimStart '-'))

                    return argument text "flag" stage parameter
                | Place.Argument(stage, Slot.Parameter parameter) ->
                    return argument text "argument" stage (Some parameter)
                | Place.Argument(stage, _) -> return argument text "argument" stage None
                | Place.Blank
                | Place.TagType
                | Place.TagAttribute _
                | Place.Unknown -> return None
        }
