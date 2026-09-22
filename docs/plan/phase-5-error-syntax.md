# Phase 5: errors in the language

**Goal.** A user recovers from a failure without leaving the line, binds a fault as a
value, and unwraps an option with a default. Decision
[0014](../decisions/0014-recovery-operator.md).

**Record to add first.** [0023](../decisions/0023-adjacent-function-parenthesis.md):
the function form requires an adjacent `(`; `name (` is a command with a parenthesised
operand. (This document first numbered it 0019, which Phase 3 took for reserved words;
records are numbered in the order they are taken.)

## Grammar (Phase 5 delta)

```
Line      ::= Pipeline ( "else" Pipeline )*
Pipeline  ::= Stage ( "|" Stage )*
Stage     ::= "try"? CommandExpression ( "??" Operand )?
CommandExpression ::= FunctionExpression | CliExpression | InstanceTag | "(" Pipeline ")"
FunctionExpression ::= Identifier "(" ...            -- no whitespace before "("
```

`else` binds looser than `|`: `a | b else c | d` is `(a | b) else (c | d)`. `try` and
`??` apply to one stage, so `try cat x | set problem` binds the fault and
`first (ls) ?? "none"` defaults what `first` returned. A parenthesised pipeline may
stand as a stage or as an operand.

## Semantics

- `else`: evaluate the left pipeline; on `Ok v` the line's value is `v` and the right
  side is not evaluated; on `Error f` evaluate the right pipeline with the fault as its
  pipe input, so `cat x else echo` prints the fault and `cat x else set problem` binds
  it. The committed events are those of the branch that produced the value.
- `try`: the stage's `Ok v` yields `v`; its `Error f` yields `Value.Fault f` as a
  successful result, which the next stage receives as input. Events of a failed
  `try` stage are discarded.
- `??`: if the stage's result is `None` or `Empty`, the operand's value; otherwise the
  result.
- Faults as values: `Value.Fault` displays as its message. `message`, `kind` and
  `path` are readable through member access on a variable: `$r.message`. `is-fault`
  returns `Boolean`.
- Nested pipeline operands `( ... )` evaluate in the same working projection and their
  events join the line's transaction.

## Tests

- Parser: precedence of `else` versus `|`; `try` position; `??` associativity;
  `first (ls)` versus `first(ls)`; round trips.
- Core: every semantic rule above, including that a failed left branch of `else`
  commits nothing and the right branch's events commit; `try` discards events;
  `$r.message` after `try`.
- Browser check: `run examples/resilient.clr` completes; `cat missing.txt else echo
  "none"` -> `none`; `try cat missing.txt | set r` then `echo $r.kind` -> `NotFound`;
  `first (ls | where $row.kind eq note) ?? "no notes"` -> `no notes`.
- `ExampleProgramTests`: every golden result of `resilient.clr`, and afterwards no
  folder named `today` exists.

## Documentation

`docs/language.md` "Errors as values"; `docs/errors.md` intro rewritten around faults
and recovery; `docs/spec/execution-model.md` the railway rules; `docs/spec/lexical-grammar.md`.
