# 0053. The language is a programming language, functional first

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |
| Supersedes | The design doc's non-goal "a general-purpose programming language", and one consequence of [0020](0020-scripts-and-run.md) |

## Context

The design doc listed "a general-purpose programming language" among its non-goals:
"There are no loops, no user-defined functions and no arithmetic." Decision
[0020](0020-scripts-and-run.md) made a script a file of lines run one at a time, and
rejected a script language for the first release as "a different project", adding that
it was "a later decision to take and not one this forecloses".

The [vision](../vision.md) asks for a functional-style scripting language. The owner
has now said what that means: loops, user-defined functions and arithmetic are all
desired and necessary, and functional constructs are more desirable still, because they
build the pipelines this environment is for.

## Options

1. **Keep the non-goal.** Scripts stay recordings, and every computation is a command
   the core has to add.
2. **A small language for shaping values only**: definitions, per-row expressions and
   folds, and still no loops. The vision's first proposal.
3. **A general-purpose language, functional first.** Loops, user-defined functions and
   arithmetic are part of the language. Functional constructs come first: functions as
   values, composition, mapping, filtering and folding, and pipelines kept to run later.

## Decision

Option 3, chosen by the owner. The non-goal is withdrawn. The language will have:

- **arithmetic** on numbers;
- **user-defined functions**, with parameters, that can be called like commands and
  passed as values;
- **loops**;
- **functional constructs** first among them: a pipeline or function as a value,
  composition, and mapping, filtering and folding over tables and lists.

The notations are not decided here. Each (how a pipeline is written without running it,
how arithmetic and conditions are spelled, how a function is defined and a loop
written) is its own record, keeping to the rules already decided: word operators
([0007](0007-notation-conflicts.md)), reserved words chosen in one go
([0019](0019-reserved-words-in-expression-positions.md)), failure as a value
([0006](0006-functional-core-in-fsharp.md)), and one transaction per line
([0015](0015-atomic-lines.md)).

## Consequences

The design doc moves the language from its non-goals to its goals. 0020 stands for
`.clr` files, which are still run one line at a time; its sentence "there is no control
flow, so a script is a recording rather than a program" no longer holds. Whether a
function's body may span several lines is left to the record that defines functions,
and the design doc's "multi-line syntax" non-goal is revisited there if one line is not
enough.
