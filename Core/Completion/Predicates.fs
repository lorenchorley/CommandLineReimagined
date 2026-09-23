/// Inside a predicate: operands, operators, and the values a column holds (stream E).
namespace CommandLineReimagined.Core

[<RequireQualifiedAccess>]
module PredicateCompletion =

    /// <summary>What the word inside a predicate could be.</summary>
    /// <remarks>
    /// Stream E dispatches on the expression state: `$row.`, `not` and `(` for an
    /// operand, the comparison operators after one, the column's values on the right,
    /// `and` and `or` after a comparison. Until then, the lexical rules.
    /// </remarks>
    let suggest (request: Request) (stage: Stage) (expression: Expression) : Async<Completion list> =
        ignore (stage, expression)
        async.Return(Lexical.answer request)
