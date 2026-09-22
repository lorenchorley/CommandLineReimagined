/// Expressions: building one from what was written, and evaluating one.
///
/// The type lives in `Values.fs`, because `Value.Query` names it. This is everything
/// else: turning the parser's nodes into it, reading a member off a value, and the
/// comparison rules that decide whether `lt` means "smaller number" or "earlier word".
///
/// Evaluation is pure and synchronous. A predicate runs once per row, and a row is a
/// scope with `$row` bound in it (decision 0008), so there is nothing to await and
/// nothing to change.
namespace CommandLineReimagined.Core

open System
open System.Globalization

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Expr =

    /// The comparison words. `and`, `or` and `not` have cases of their own, because
    /// they combine expressions rather than comparing values.
    let comparisonOperators = [ "eq"; "ne"; "gt"; "ge"; "lt"; "le"; "like"; "has" ]

    // --------------------------------------------------------- From what was written

    /// Whether a written argument actually used an operator, as opposed to being a
    /// plain operand. This is what the binder asks to decide whether an argument is a
    /// predicate or an ordinary value.
    let isExpression (node: Tree.Value) =
        match node with
        | :? Tree.ExpressionNode -> true
        | _ -> false

    /// <summary>Whether the expression asks a question rather than naming a thing.</summary>
    /// <remarks>
    /// The same distinction as `isExpression`, after the tree has been left behind.
    /// `cd` is the reason it exists: one parameter that is a folder path when it is a
    /// plain operand and a view when it has an operator in it (decision 0013).
    /// </remarks>
    let isPredicate (expr: Expr) =
        match expr with
        | Expr.Compare _
        | Expr.And _
        | Expr.Or _
        | Expr.Not _ -> true
        | Expr.Const _
        | Expr.Variable _
        | Expr.Nested _ -> false

    /// <summary>Turns a parsed argument into an expression.</summary>
    /// <remarks>
    /// Total over the nodes an operand can be, and a `Binding` fault over the ones it
    /// cannot: a tag has no meaning in a comparison, and saying so names the mistake
    /// better than evaluating it to its display string would.
    /// </remarks>
    let rec ofNodeWith (nested: Tree.NestedPipeline -> Outcome<Expr>) (node: Tree.Value) : Outcome<Expr> =
        let ofNode = ofNodeWith nested

        match node with
        | null -> Ok(Expr.Const Value.Empty)

        | :? Tree.StringConstant as text -> Ok(Expr.Const(Value.Text text.Value))

        | :? Tree.Identifier as identifier -> Ok(Expr.Const(Value.ofWord identifier.Name))

        | :? Tree.VariableReference as reference ->
            Ok(Expr.Variable(reference.Name.Name, reference.Members |> Seq.map (fun m -> m.Name) |> List.ofSeq))

        | :? Tree.ComparisonExpression as comparison ->
            outcome {
                let! left = ofNode comparison.Left
                let! right = ofNode comparison.Right
                return Expr.Compare(comparison.Operator.Name, left, right)
            }

        | :? Tree.BooleanExpression as combination ->
            outcome {
                let! left = ofNode combination.Left
                let! right = ofNode combination.Right

                return
                    if combination.Operator.Name = "or" then
                        Expr.Or(left, right)
                    else
                        Expr.And(left, right)
            }

        | :? Tree.NotExpression as negation -> ofNode negation.Operand |> Outcome.map Expr.Not

        | :? Tree.NestedPipeline as pipeline -> nested pipeline

        | other -> Error(Fault.notAnOperand (other.GetType().Name))

    /// <summary>Turns a parsed argument into an expression, keeping any nested pipeline as written.</summary>
    /// <remarks>
    /// The evaluator runs a line's nested pipelines before binding and uses
    /// `ofNodeWith` to put their values in; this form is for an expression with no line
    /// around it, such as a saved view read back from its file.
    /// </remarks>
    let ofNode (node: Tree.Value) : Outcome<Expr> =
        ofNodeWith (fun pipeline -> Ok(Expr.Nested pipeline.Pipeline)) node

    // ------------------------------------------------------------------- Members

    /// <summary>Reads `.name` off a value.</summary>
    /// <remarks>
    /// A name the value does not have is `None` rather than a failure, because a sparse
    /// table's gaps are exactly that (decision 0009) and a predicate over a table where
    /// one row is missing a column should skip that row, not stop the line.
    /// </remarks>
    let readMember (name: string) (value: Value) : Value =
        match value with
        | Value.Object tag
        | Value.Component tag ->
            match Map.tryFind name tag.Attributes with
            | Some found -> found
            | Option.None -> Value.None

        | Value.File file ->
            match name with
            | "name" -> Value.Text file.Name
            | "kind" -> Value.Text file.Kind
            | "folder" -> Value.Text file.Folder
            | "path" -> Value.Text(Value.joinPath file.Folder file.Name)
            | "id" -> Value.Text file.Id
            | _ -> Value.None

        // Decision 0014: a fault held as a value can be asked what it was. `kind` is
        // the word a script compares against, `message` the sentence a person reads.
        | Value.Fault fault ->
            match name with
            | "message" -> Value.Text fault.Message
            | "kind" -> Value.Text(FaultKind.name fault.Kind)
            | "path" ->
                match fault.Path with
                | Some path -> Value.Text path
                | Option.None -> Value.None
            | "stage" ->
                match fault.Stage with
                | Some stage -> Value.Number(float stage)
                | Option.None -> Value.None
            | "cause" ->
                match fault.Cause with
                | Some cause -> Value.Fault cause
                | Option.None -> Value.None
            | _ -> Value.None

        | _ -> Value.None

    // --------------------------------------------------------------- Comparison

    let private asNumber (value: Value) : float option =
        match value with
        | Value.Number n -> Some n
        | Value.Text text ->
            match Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture) with
            | true, n -> Some n
            | _ -> Option.None
        | _ -> Option.None

    /// <summary>`*` as "anything", anchored at both ends, matched without case.</summary>
    let private glob (pattern: string) (text: string) =
        let escaped =
            pattern.Split '*'
            |> Array.map Text.RegularExpressions.Regex.Escape
            |> String.concat ".*"

        Text.RegularExpressions.Regex.IsMatch(
            text,
            "^" + escaped + "$",
            Text.RegularExpressions.RegexOptions.IgnoreCase)

    let private contains (item: Value) (value: Value) =
        let wanted = Value.display item

        match value with
        | Value.List items -> items |> List.exists (fun each -> Value.display each = wanted)
        | Value.Table table -> table.Rows |> List.exists (List.exists (fun cell -> Value.display cell = wanted))
        | Value.Object tag
        | Value.Component tag ->
            tag.Attributes |> Map.exists (fun _ each -> Value.display each = wanted)
        | other -> (Value.display other).Contains(wanted, StringComparison.Ordinal)

    /// <summary>Which of two values comes first.</summary>
    /// <remarks>
    /// The one ordering the language has: numbers numerically when both sides read as
    /// numbers, display strings ordinally otherwise. `sort` uses it and so does every
    /// comparison, so a column that sorts with `9` before `100` compares that way too.
    /// An absent value comes before everything, which puts a table's gaps at the top of
    /// an ascending sort rather than scattered through it.
    /// </remarks>
    let order (left: Value) (right: Value) : int =
        match Value.isAbsent left, Value.isAbsent right with
        | true, true -> 0
        | true, false -> -1
        | false, true -> 1
        | false, false ->
            match asNumber left, asNumber right with
            | Some a, Some b -> compare a b
            | _ -> String.CompareOrdinal(Value.display left, Value.display right)

    /// <summary>Compares two values with one of the eight comparison words.</summary>
    /// <remarks>
    /// Numbers compare numerically when both sides are numbers — including text that
    /// reads as one, so `where $row.size gt 100` works on a column that came out of an
    /// XML file as text. Anything else compares its display string ordinally, which is
    /// what makes `sort name` and `eq folder` agree with what is on screen.
    ///
    /// A comparison that touches an absent value is `false`, with one exception: two
    /// absent values are `eq`. A gap is not smaller than 100, nor larger, nor equal to
    /// it, and answering `false` to all three is the only consistent thing to do.
    /// </remarks>
    let compareValues (op: string) (left: Value) (right: Value) : Outcome<Value> =
        let absent = Value.isAbsent left || Value.isAbsent right

        if absent then
            Ok(Value.Boolean(op = "eq" && Value.isAbsent left && Value.isAbsent right))
        else
            let ordering () = order left right

            match op with
            | "eq" -> Ok(Value.Boolean(ordering () = 0))
            | "ne" -> Ok(Value.Boolean(ordering () <> 0))
            | "gt" -> Ok(Value.Boolean(ordering () > 0))
            | "ge" -> Ok(Value.Boolean(ordering () >= 0))
            | "lt" -> Ok(Value.Boolean(ordering () < 0))
            | "le" -> Ok(Value.Boolean(ordering () <= 0))
            | "like" ->
                let pattern = Value.display right
                let text = Value.display left

                Ok(
                    Value.Boolean(
                        if pattern.Contains '*' then
                            glob pattern text
                        else
                            text.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            | "has" -> Ok(Value.Boolean(contains right left))
            | other -> Error(Fault.unknownOperator other)

    // --------------------------------------------------------------- Evaluation

    /// Whether a value counts as true. Only `Boolean true` does: `where` keeps the rows
    /// its predicate answered true for, and a row whose predicate answered "notes.txt"
    /// has not been asked a question that was answered.
    let isTrue (value: Value) =
        match value with
        | Value.Boolean b -> b
        | _ -> false

    /// <summary>Evaluates an expression against a scope.</summary>
    /// <remarks>
    /// `and` and `or` are short circuiting, so an unknown variable on the right of a
    /// false `and` is never looked up.
    /// </remarks>
    let rec evaluate (scope: Scope) (expr: Expr) : Outcome<Value> =
        match expr with
        | Expr.Const value -> Ok value

        | Expr.Variable(name, members) ->
            scope.TryFind name
            |> Outcome.ofOption (Fault.unknownVariable name)
            |> Outcome.map (fun value -> members |> List.fold (fun current m -> readMember m current) value)

        | Expr.Compare(op, left, right) ->
            outcome {
                let! a = evaluate scope left
                let! b = evaluate scope right
                return! compareValues op a b
            }

        | Expr.And(left, right) ->
            outcome {
                let! a = evaluate scope left

                if not (isTrue a) then
                    return Value.Boolean false
                else
                    let! b = evaluate scope right
                    return Value.Boolean(isTrue b)
            }

        | Expr.Or(left, right) ->
            outcome {
                let! a = evaluate scope left

                if isTrue a then
                    return Value.Boolean true
                else
                    let! b = evaluate scope right
                    return Value.Boolean(isTrue b)
            }

        | Expr.Not operand -> evaluate scope operand |> Outcome.map (fun value -> Value.Boolean(not (isTrue value)))

        // A pipeline is the evaluator's to run, and it runs a line's nested pipelines
        // before the predicate is built, so one only survives to here from an
        // expression that no line ran — a saved view read back from its file.
        | Expr.Nested pipeline -> Error(Fault.nestedPipelineNotAValue (Value.exprText (Expr.Nested pipeline)))

    /// The display text of a predicate, for `Value.display` and for the prompt.
    let display (expr: Expr) = Value.exprText expr

    /// <summary>Reads a predicate back from text.</summary>
    /// <remarks>
    /// A saved view is a file whose content is the predicate as it was written
    /// (decision 0013), so entering one means parsing it again. It goes through the
    /// same grammar the line went through, which is what makes `save-view` and `cd`
    /// agree about what the text meant.
    /// </remarks>
    let parse (path: string) (text: string) : Outcome<Expr> =
        let parsed = CommandLineReimagined.Parsing.CommandLineParser().Parse<Tree.Value> text

        parsed.Match(
            (fun (node: Tree.Value) -> ofNode node),
            (fun _ -> Error(Fault.notAPredicate path (text.Trim()))))
