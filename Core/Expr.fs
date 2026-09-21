/// Expressions.
///
/// A stub in Phase 1, and deliberately so: `Location.View` is an `Expr option`, so the
/// type has to exist before the store does, and a location that can hold a query is
/// what makes Phase 4 an addition rather than a change to the event shape.
///
/// Phase 3 fills this in with comparisons, the boolean words and `$row` member access.
namespace CommandLineReimagined.Core

type Expr =
    | Const of Value
    /// A column of the row under consideration: the `kind` in `$row.kind`.
    | Column of string
