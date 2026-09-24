/// Inside a predicate: operands, operators, and the values a column holds (stream E).
///
/// The lexical rules guessed whether a predicate was being written from a `$` earlier
/// in the stage, and then offered all eleven word operators wherever one might go.
/// The line has been read by now, so the place says which part of the expression the
/// word is, and each part has one kind of answer.
namespace CommandLineReimagined.Core

open System

[<RequireQualifiedAccess>]
module PredicateCompletion =

    /// How many of a column's values are offered at most.
    let maxValues = 12

    /// The words that join two questions, offered after a whole comparison.
    let combiners = [ "and"; "or" ]

    let private startsWith (prefix: string) (text: string) =
        text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)

    let private matching (request: Request) (completions: Completion list) =
        completions |> List.filter (fun completion -> startsWith request.Context.Word.Prefix completion.Text)

    /// Where a value starts: the row's columns, a negation, or a group.
    let private operand (request: Request) =
        [ Request.item request "member" "$row." |> Request.withDetail "a column of the row being tested"
          Request.item request "operator" "not" |> Request.withDetail "true where what follows is false"
          Request.item request "operator" "(" |> Request.withDetail "a group, or a pipeline to compare with" ]
        |> matching request

    /// After an operand: the eight words that compare two values.
    let private comparison (request: Request) =
        Expr.comparisonOperators |> List.map (Request.item request "operator") |> matching request

    /// After a comparison: the words that join it to another.
    let private combination (request: Request) =
        combiners |> List.map (Request.item request "operator") |> matching request

    /// <summary>The values a column holds, most frequent first.</summary>
    /// <remarks>
    /// Read from the rows that flow into the stage, which are only there when the
    /// stages before it were run (decision 0031). Until they are, there is nothing to
    /// offer rather than a guess. For `like`, each value is a pattern: it and anything
    /// after it.
    /// </remarks>
    let private values (request: Request) (stage: Stage) (op: string) (column: string) =
        async {
            let! shape = request.Shapes stage

            match shape.Rows with
            | None -> return []
            | Some rows ->
                let word = request.Context.Word

                let counted =
                    rows
                    |> List.choose (Map.tryFind column)
                    |> List.filter (Value.isAbsent >> not)
                    |> List.map Value.display
                    |> List.filter (fun shown -> shown <> "")
                    |> List.countBy id
                    |> List.sortWith (fun (a, m) (b, n) -> if m <> n then compare n m else String.CompareOrdinal(a, b))

                return
                    counted
                    |> List.map (fun (shown, count) -> (if op = "like" then shown + "*" else shown), count)
                    |> List.filter (fun (shown, _) -> startsWith word.Prefix shown)
                    |> List.truncate maxValues
                    |> List.map (fun (shown, count) ->
                        let text = if word.Quoted then "\"" + shown + "\"" else Expr.asWritten shown

                        Request.item request "value" text
                        |> Request.withDetail (if count = 1 then "1 row" else sprintf "%d rows" count))
        }

    /// <summary>Whether the word is the first thing written in `in`'s argument.</summary>
    /// <remarks>
    /// There a plain operand is a path (decision 0013), so the word names a place.
    /// After `not` or `and` it is part of a question, and is answered as one.
    /// </remarks>
    let private startsInArgument (request: Request) (stage: Stage) =
        let before = request.Context.Text.Substring(0, request.Context.Word.Start)

        let lastWritten =
            before.Split([| ' '; '\t'; '|'; '(' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.tryLast
            |> Option.defaultValue ""

        String.Equals(stage.Name, "in", StringComparison.OrdinalIgnoreCase)
        && String.Equals(lastWritten, stage.Name, StringComparison.OrdinalIgnoreCase)

    /// <summary>What the word inside a predicate could be.</summary>
    /// <remarks>
    /// Dispatches on the expression state: `$row.`, `not` and `(` for an operand, the
    /// comparison operators after one, the column's values on the right, `and` and `or`
    /// after a comparison. `in`'s operand is the exception: a plain one is a path, so
    /// it offers the folders and views a plain operand names, as it always has.
    /// </remarks>
    let suggest (request: Request) (stage: Stage) (expression: Expression) : Async<Completion list> =
        match expression with
        | Expression.Operand when startsInArgument request stage -> async.Return(PathCompletion.suggest request true)
        | Expression.Operand -> async.Return(operand request)
        | Expression.AfterOperand _ -> async.Return(comparison request)
        | Expression.ComparisonRight(op, Expr.Variable("row", [ column ])) -> values request stage op column
        | Expression.ComparisonRight _ -> async.Return []
        | Expression.AfterComparison -> async.Return(combination request)
