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
    /// `in` is the reason it exists: one parameter that is a folder path when it is a
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

    /// The display text of a predicate, for `Value.display` and for the prompt.
    let display (expr: Expr) = Value.exprText expr

    // ------------------------------------------------ A question about the row (0033)

    /// <summary>Whether the expression reads `$row` anywhere.</summary>
    /// <remarks>
    /// A nested pipeline does not count: it runs once for the line, not once per row,
    /// so a `$row` inside it is the inner predicate's and not this one's.
    /// </remarks>
    let rec readsTheRow (expr: Expr) =
        match expr with
        | Expr.Variable(name, _) -> name = "row"
        | Expr.Compare(_, left, right)
        | Expr.And(left, right)
        | Expr.Or(left, right) -> readsTheRow left || readsTheRow right
        | Expr.Not operand -> readsTheRow operand
        | Expr.Const _
        | Expr.Nested _ -> false

    /// A bare word that could be a column's name, read as that column.
    let private asColumn (expr: Expr) =
        match expr with
        | Expr.Const(Value.Text word) when
            word.Length > 0
            && (Char.IsLetter word[0] || word[0] = '_')
            && word |> Seq.forall (fun c -> Char.IsLetterOrDigit c || c = '_')
            ->
            Some(Expr.Variable("row", [ word ]))
        | _ -> Option.None

    /// <summary>The predicate with the bare words it compared read as columns.</summary>
    /// <remarks>
    /// In a comparison it is the left side that names the column (`kind eq folder`),
    /// unless the left is not a word and the right is (`3 lt size`). An operand of
    /// `and`, `or` or `not` that is a bare word is read as a column too: `not done`.
    /// </remarks>
    let rec private withColumns (question: bool) (expr: Expr) =
        match expr with
        | Expr.Compare(op, left, right) ->
            match asColumn left, asColumn right with
            | Some column, _ -> Expr.Compare(op, column, right)
            | Option.None, Some column -> Expr.Compare(op, left, column)
            | Option.None, Option.None -> expr
        | Expr.And(left, right) -> Expr.And(withColumns true left, withColumns true right)
        | Expr.Or(left, right) -> Expr.Or(withColumns true left, withColumns true right)
        | Expr.Not operand -> Expr.Not(withColumns true operand)
        | other when question -> asColumn other |> Option.defaultValue other
        | other -> other

    /// <summary>The static half of decision 0033, checked when a predicate is bound.</summary>
    /// <remarks>
    /// A predicate that uses an operator and never reads `$row` is the same for every
    /// row, so it is a binding fault that names the bare words it compared as the
    /// likely columns. A plain operand is not checked: `in documents` is a path
    /// (decision 0013), and `where $flag` asks nothing of the rows but is not wrong.
    /// </remarks>
    let asksAboutTheRow (expr: Expr) : Outcome<Expr> =
        if not (isPredicate expr) || readsTheRow expr then
            Ok expr
        else
            let suggested = withColumns true expr

            let suggestion =
                if readsTheRow suggested then Some(display suggested) else Option.None

            Error(Fault.neverReadsTheRow (display expr) suggestion)

    /// A value as it would be written on the right of `eq`: quoted when it would not
    /// read back as one word. Completion writes a column's values the same way.
    let asWritten (shown: string) =
        if shown = "" || shown |> Seq.exists (fun c -> Char.IsWhiteSpace c || "|()<>\"$,".Contains c) then
            "\"" + shown.Replace("\"", "'") + "\""
        else
            shown

    /// What a value displays as, on one line and not too long to read in a message.
    let private briefly (value: Value) =
        let shown = (Value.display value).Split('\n').[0].TrimEnd()
        if shown.Length > 40 then shown.Substring(0, 39) + "…" else shown

    /// <summary>What a value says as the answer to a yes-or-no question.</summary>
    /// <remarks>
    /// `true` and `false` answer it. So does the word `true` or `false`, in any case,
    /// because `attr x done=true` stores the word and `where $row.done` is plainly
    /// asking whether it is done (decision 0034). A gap is `false`, because a sparse
    /// table's gap is exactly that (decision 0009): `where $row.done` skips a row with
    /// no `done` rather than stopping the line. Anything else was never an answer, and
    /// the fault names the expression it came from, what it was, and how to ask a
    /// question of it. The same rule holds for a whole predicate and for each operand
    /// of `and`, `or` and `not`.
    /// </remarks>
    let truth (expr: Expr) (value: Value) : Outcome<bool> =
        match value with
        | Value.Boolean answer -> Ok answer
        | absent when Value.isAbsent absent -> Ok false
        | Value.Text word when String.Equals(word, "true", StringComparison.OrdinalIgnoreCase) -> Ok true
        | Value.Text word when String.Equals(word, "false", StringComparison.OrdinalIgnoreCase) -> Ok false
        | other ->
            let shown = briefly other

            // What to write instead, said in a note with the fix that writes it
            // (decisions 0041, 0044) rather than in the fault's sentence.
            let suggestion =
                if readsTheRow expr then
                    let compared = sprintf "%s eq %s" (display expr) (asWritten shown)
                    Some(sprintf "Compare it: %s." compared, Fix.Replace(display expr, compared))
                else
                    match asColumn expr with
                    | Some column ->
                        Some(sprintf "Did you mean %s?" (display column), Fix.Replace(display expr, display column))
                    | Option.None -> Option.None

            Error(Fault.notTrueOrFalse (display expr) (Value.kind other) shown suggestion)

    // --------------------------------------------------------------- Evaluation

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
                let! a = evaluate scope left |> Outcome.bind (truth left)

                if not a then
                    return Value.Boolean false
                else
                    let! b = evaluate scope right |> Outcome.bind (truth right)
                    return Value.Boolean b
            }

        | Expr.Or(left, right) ->
            outcome {
                let! a = evaluate scope left |> Outcome.bind (truth left)

                if a then
                    return Value.Boolean true
                else
                    let! b = evaluate scope right |> Outcome.bind (truth right)
                    return Value.Boolean b
            }

        | Expr.Not operand ->
            evaluate scope operand
            |> Outcome.bind (truth operand)
            |> Outcome.map (fun answer -> Value.Boolean(not answer))

        // A pipeline is the evaluator's to run, and it runs a line's nested pipelines
        // before the predicate is built, so one only survives to here from an
        // expression that no line ran — a saved view read back from its file.
        | Expr.Nested pipeline -> Error(Fault.nestedPipelineNotAValue (Value.exprText (Expr.Nested pipeline)))

    /// <summary>Tests one row: the dynamic half of decision 0033.</summary>
    /// <remarks>The rule for what counts as an answer is `truth`'s.</remarks>
    let test (scope: Scope) (expr: Expr) : Outcome<bool> =
        evaluate scope expr |> Outcome.bind (truth expr)

    /// <summary>Reads a predicate back from text.</summary>
    /// <remarks>
    /// A saved view is a file whose content is the predicate as it was written
    /// (decision 0013), so entering one means parsing it again. It goes through the
    /// same grammar the line went through, which is what makes `save-view` and `in`
    /// agree about what the text meant.
    /// </remarks>
    let parse (path: string) (text: string) : Outcome<Expr> =
        let parsed = CommandLineReimagined.Parsing.CommandLineParser().Parse<Tree.Value> text

        parsed.Match(
            (fun (node: Tree.Value) -> ofNode node),
            (fun _ -> Error(Fault.notAPredicate path (text.Trim()))))

    // ------------------------------------------- Why a filter kept nothing (0043)

    /// `a`, `a or b`, `a, b or c`: how a note names several things.
    let private inWords (items: string list) =
        match List.rev items with
        | [] -> ""
        | [ only ] -> only
        | last :: before -> String.concat ", " (List.rev before) + " or " + last

    /// <summary>The columns a predicate reads through `$row.`, in the order written.</summary>
    /// <remarks>
    /// Each with the members read after it, so a fix can write `$row.name.length` back
    /// whole. A nested pipeline's `$row` is its own predicate's, as in `readsTheRow`.
    /// </remarks>
    let rec private rowReads (expr: Expr) : (string * string list) list =
        match expr with
        | Expr.Variable("row", column :: rest) -> [ column, rest ]
        | Expr.Compare(_, left, right)
        | Expr.And(left, right)
        | Expr.Or(left, right) -> rowReads left @ rowReads right
        | Expr.Not operand -> rowReads operand
        | Expr.Variable _
        | Expr.Const _
        | Expr.Nested _ -> []

    /// <summary>The cells of a column that are not gaps, row by row.</summary>
    /// <remarks>
    /// The name is matched exactly, because that is how `$row.` reads it: a row is a
    /// tag, and `$row.Kind` does not read `kind`. A column the table lists but no row
    /// fills is one no row has (decision 0009: a gap is an attribute that is not there).
    /// </remarks>
    let private cellsOf (table: Table) (column: string) : Value list =
        match table.Columns |> List.tryFindIndex (fun c -> c.Name = column) with
        | Option.None -> []
        | Some index ->
            table.Rows
            |> List.choose (fun row ->
                match List.tryItem index row with
                | Some cell when not (Value.isAbsent cell) -> Some cell
                | _ -> Option.None)

    /// <summary>`No row has knd; did you mean kind?`, with the fix for the nearest.</summary>
    /// <remarks>
    /// The nearest of the columns some row has, at most three as decision 0042 counts
    /// them, and a fix for the first only (0043). A column that differs only in case is
    /// named first: `Nearest` counts it no slip at all, and it is still not what `$row.`
    /// read. With nothing near, the sentence ends at the column.
    /// </remarks>
    let private noRowHas nearest (had: string list) (column: string, members: string list) =
        let sameButCase =
            had
            |> List.filter (fun name ->
                name <> column && String.Equals(name, column, StringComparison.OrdinalIgnoreCase))

        match sameButCase @ nearest had column |> List.distinct |> List.truncate 3 with
        | [] -> Note.explanation (sprintf "No row has %s." column) []
        | first :: _ as near ->
            let written = String.concat "." ("$row" :: column :: members)
            let corrected = String.concat "." ("$row" :: first :: members)

            Note.explanation
                (sprintf "No row has %s; did you mean %s?" column (inWords near))
                [ Fix.Replace(written, corrected) ]

    /// <summary>`kind is folder or text`: the values a column does have.</summary>
    /// <remarks>
    /// Distinct by what they display as, the way `eq` and `group` compare them; most
    /// frequent first, ties in the order the rows had them; written as they would be on
    /// the right of `eq`. At most five are named. With more, the sentence says how many
    /// it left out rather than ending on one that is not the last:
    /// `name is a, b, c, d, e or 3 more`.
    /// </remarks>
    let private valuesOf (column: string) (cells: Value list) =
        let shown = cells |> List.map Value.display
        let counts = shown |> List.countBy id |> Map.ofList

        let ranked =
            shown
            |> List.distinct
            |> List.sortBy (fun each -> -(Map.find each counts))
            |> List.map (fun each -> asWritten (briefly (Value.Text each)))

        let named = List.truncate 5 ranked

        match List.length ranked - List.length named with
        | 0 -> sprintf "%s is %s" column (inWords named)
        | more -> sprintf "%s is %s or %d more" column (String.concat ", " named) more

    /// <summary>The fix for an `eq` whose value is one slip from one a row has (decision 0045).</summary>
    /// <remarks>
    /// The nearest of the values the column has, by `nearest`, written as it would be on
    /// the right of `eq`. Only for a value written in the line: one held in a variable
    /// has no place in the line to correct, and offers none.
    /// </remarks>
    let private nearValue nearest (other: Expr) (compared: Value) (cells: Value list) : Fix list =
        match other with
        | Expr.Const _ ->
            let had = cells |> List.map Value.display |> List.distinct

            match nearest had (Value.display compared) with
            | first :: _ -> [ Fix.Replace(asWritten (Value.display compared), asWritten first) ]
            | [] -> []
        | _ -> []

    /// <summary>`size runs from 0 to 1361`: the span of a column of numbers.</summary>
    /// <remarks>
    /// What explains `gt`, `ge`, `lt` and `le` keeping nothing, which a list of values
    /// would not: the question was about where the numbers lie. Only when every cell and
    /// the value compared read as numbers; an ordering of words is not explained.
    /// </remarks>
    let private spanOf (column: string) (cells: Value list) (compared: Value) =
        match asNumber compared, cells |> List.map asNumber with
        | Some _, numbers when not (List.isEmpty numbers) && numbers |> List.forall Option.isSome ->
            let numbers = numbers |> List.choose id
            let low = Value.display (Value.Number(List.min numbers))
            let high = Value.display (Value.Number(List.max numbers))

            if low = high then
                Some(sprintf "%s is %s" column low)
            else
                Some(sprintf "%s runs from %s to %s" column low high)
        | _ -> Option.None

    /// <summary>A comparison of one column with something that is not the row.</summary>
    /// <remarks>
    /// Turned so the column is on the left: `100 lt $row.size` is asked as
    /// `$row.size gt 100`. `like` and `has` are not turned: their sides are not
    /// interchangeable. A read deeper than the column (`$row.name.length`) is not the
    /// column's values, and is not explained.
    /// </remarks>
    let private comparison (expr: Expr) =
        let turned op =
            match op with
            | "eq"
            | "ne" -> Some op
            | "gt" -> Some "lt"
            | "lt" -> Some "gt"
            | "ge" -> Some "le"
            | "le" -> Some "ge"
            | _ -> Option.None

        match expr with
        | Expr.Compare(op, Expr.Variable("row", [ column ]), other) when not (readsTheRow other) ->
            Some(op, column, other)
        | Expr.Compare(op, other, Expr.Variable("row", [ column ])) when not (readsTheRow other) ->
            turned op |> Option.map (fun op -> op, column, other)
        | _ -> Option.None

    /// The operands of the `and`s at the top of a predicate: any one of them keeping no
    /// row is enough for the whole to keep none.
    let rec private conjuncts (expr: Expr) =
        match expr with
        | Expr.And(left, right) -> conjuncts left @ conjuncts right
        | other -> [ other ]

    /// <summary>Why a filter that read a table with rows kept none of them (decision 0043).</summary>
    /// <remarks>
    /// Asked by `where`, `find` and a view's listing when nothing was kept; the answer
    /// is at most one `explanation` note, and the empty table stays the answer.
    /// `nearest` is the rule for near names (`Nearest.names`); `scope` is the line's,
    /// for a compared value that is a variable.
    ///
    /// First, a column the predicate reads through `$row.`, anywhere in it, that no row
    /// has: the first one written, with the nearest columns (`noRowHas`). One at a time,
    /// because a fix changes one place and the next run names the next. Otherwise, the
    /// first operand of the predicate's top-level `and`s that compares one column with
    /// a value not read off the row, and that on its own keeps no row:
    ///
    /// - `eq` and `like` answer the values the column does have (`valuesOf`), and an
    ///   `eq` whose value is near one of them offers it as a fix (`nearValue`, 0045);
    /// - `gt`, `ge`, `lt` and `le` over numbers answer their span (`spanOf`);
    /// - `ne` and `has` are not explained: `ne` keeping nothing means every row holds
    ///   the one value, which the question already named, and what `has` looks inside
    ///   is not a column's values.
    ///
    /// Anything else stays silent: an `or` or a `not` keeping nothing has no one part to
    /// name, and a bare `$row.done` has no value it was compared with. So does a table
    /// with no rows: an empty answer to an empty table needs no reason.
    /// </remarks>
    let explainEmpty
        (nearest: string list -> string -> string list)
        (scope: Scope)
        (table: Table)
        (expr: Expr)
        : Note list =
        if List.isEmpty table.Rows then
            []
        else
            let had =
                table.Columns
                |> List.map (fun c -> c.Name)
                |> List.filter (fun name -> not (List.isEmpty (cellsOf table name)))

            let missing =
                rowReads expr |> List.tryFind (fun (column, _) -> not (List.contains column had))

            match missing with
            | Some read -> [ noRowHas nearest had read ]
            | Option.None ->
                let explain part =
                    match comparison part with
                    | Option.None -> Option.None
                    | Some(op, column, other) ->
                        match evaluate scope other with
                        | Error _ -> Option.None
                        | Ok compared ->
                            let cells = cellsOf table column

                            let keepsNone =
                                cells
                                |> List.forall (fun cell ->
                                    match compareValues op cell compared with
                                    | Ok(Value.Boolean false) -> true
                                    | _ -> false)

                            if not keepsNone then
                                Option.None
                            else
                                match op with
                                | "eq" -> Some(valuesOf column cells, nearValue nearest other compared cells)
                                | "like" -> Some(valuesOf column cells, [])
                                | "gt"
                                | "ge"
                                | "lt"
                                | "le" -> spanOf column cells compared |> Option.map (fun text -> text, [])
                                | _ -> Option.None

                conjuncts expr
                |> List.tryPick explain
                |> Option.map (fun (text, fixes) -> Note.explanation text fixes)
                |> Option.toList
