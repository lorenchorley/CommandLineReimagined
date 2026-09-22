# Phase 3: tables, expressions and table functions

**Goal.** A `Table` value, an expression grammar with word operators and an explicit
`$row`, table functions, and a table renderer in the browser. `ls`, `vars`, `attr`,
`history` and `help` return tables. Decisions
[0007](../decisions/0007-notation-conflicts.md) (part C),
[0008](../decisions/0008-explicit-row-variable.md),
[0009](../decisions/0009-table-coercion.md).

**Records to add first.** [0019](../decisions/0019-reserved-words-in-expression-positions.md)
reserved words; [0020](../decisions/0020-scripts-and-run.md) scripts and `run`;
[0021](../decisions/0021-variadic-parameters.md) variadic parameters, which `select`
turned out to need.

## The Table value

```fsharp
type ColumnType = TextCol | NumberCol | BooleanCol | FileCol | ObjectCol | MixedCol
type Column = { Name: string; Type: ColumnType }
type Table  = { Columns: Column list; Rows: Value list list }   // each row has Columns.Length cells; None fills gaps
```

`Table.ofRecords : FileRecord list -> Table` builds the file table: columns `name`
(as `File`), `kind`, `folder`, `size` (number), `modified`, then every other attribute
present in any row, ordered by name. `Table.ofTag : Tag -> Outcome<Table>` applies
[0009](../decisions/0009-table-coercion.md): every child the same type, no
grandchildren, union of attributes, `None` in gaps, a column typed when every non-None
cell agrees. `Table.toTag : Table -> Tag` is its inverse with type name `row` unless
given.

## Grammar (Phase 3 delta in [architecture.md](architecture.md#grammar-changes-by-phase))

1. `Expr` in `Parser.Tree`: `ExprNode` cases `Operand of Value`, `Compare of op *
   ExprNode * ExprNode`, `And`, `Or`, `Not`, `Nested of PipedCommandList` (for
   `( ... )`). `CommandArgumentValue.Value` may now be an `ExpressionValue` wrapping an
   `ExprNode`; a plain operand still produces the old node so existing tests hold.
2. `VariableReference` gains `Members: string list` for `$row.size`.
3. Reserved words are not `Word`s. `echo eq` is a syntax error; `echo "eq"` is text.
4. Tokeniser: `operator` for the comparison and boolean words, `member` for `.size`.
   Serialiser round-trips.
5. `Core.Expr` becomes the evaluated form; `Binder.evaluateExpr : Scope -> ExprNode ->
   Outcome<Value>`; comparisons on numbers compare numerically, on text ordinally,
   `like` is a case-insensitive substring or `*` glob, `has` is membership in a `List`
   or a `Table` column or substring on text; comparing `None` yields `Boolean false`
   except `eq None`.

## Table functions (`Core/Commands/Tables.fs`)

All take their table from the pipe or as their first argument (an optional, piped
`table` parameter, so both `ls | first` and `first (ls)` work), coerce a table-shaped
tag or a list of same-typed objects, return a table unless stated, and are pure.

| Command | Parameters | Result |
| --- | --- | --- |
| `where` | `predicate` (Predicate) | rows for which the predicate is `Boolean true`, with `$row` bound per row |
| `select` | `columns` (names, one or more positional) | those columns in that order |
| `sort` | `column`, optional `desc` flag | stable sort by the column, numbers numerically |
| `take`, `skip` | `count` | first or remaining rows |
| `first`, `last` | none | the row as an `Object` of type `row`, or `None` |
| `count` | none | `Number` of rows |
| `distinct` | optional `column` | unique rows or unique values of a column |
| `group` | `column` | table with `key` and `rows` (a nested table per group) |
| `columns` | none | table of `name`, `type` |
| `rows` | none | `List` of `Object` rows |
| `table` | none | explicit coercion of a tag or a list of same-typed objects |

`ls` returns `Table.ofRecords` with no parent row (the page offers `up` in the
location line); `vars` a table of `name`, `value`; `attr path` a table of `name`,
`value`; `history` a table of `seq`, `at`, `source`, `undone`; `help` moves from the
page into the language as a meta command returning `name`, `parameters`,
`description`.

## The `run` command and the example programs

Implement `run` as specified in [examples.md](examples.md#the-run-command), in
`Core/Commands/Meta.fs`. Embed `examples/*.clr` in `Core.fsproj` as resources and add
them to the seed transaction as files of kind `script` under `/examples`. Add
`Core.Tests/ExampleProgramTests.fs` with the line-by-line and whole-file checks for
`tables.clr`; the other three programs are added to it by their phases.

Binding: a `Predicate` parameter receives the unevaluated `ExprNode`; the command
evaluates it per row in a child scope with `$row` bound to the row as an `Object`.

## Browser

Render `kind: "table"` as an HTML table inside the entry: header row from `columns`,
cells rendered with the existing item rules (files as chips, text as text), tap a header
to sort client-side, tap a cell to insert its text. Wide tables scroll horizontally
inside the entry. Colour `operator` and `member` tokens. Completion offers operator
words after an operand and column names after `$row.`.

## Tests

- Parser: precedence (`a or b and c`), `not`, member access, reserved words rejected
  as bare words, nested pipeline operand, round trips.
- Core: `Table.ofTag` for a well-formed tag, a sparse tag (None gaps), a tag with a
  differently typed child (fault naming the child), a tag with grandchildren (fault);
  every table function; comparison semantics incl. `None`; `ls` column set.
- Browser check: `run examples/tables.clr` completes; `ls | where $row.kind eq folder
  | count` -> `3`; `ls | sort size desc | first` renders a row object; a table renders
  with the right header.
- `ExampleProgramTests`: every golden result of `tables.clr`.

## Documentation

New `docs/tables.md` (user guide to tables, predicates and every function, with real
output); `docs/language.md` gains "Expressions" and the reserved-word list;
`docs/commands.md` gains the table functions; `docs/spec/execution-model.md` gains the
Table value and comparison rules; `docs/spec/lexical-grammar.md` the delta.
