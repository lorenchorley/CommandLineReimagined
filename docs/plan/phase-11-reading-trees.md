# Phase 11: reading a tree

**Goal.** Everything in a tag can be read: its name and children with `@`, and any
element of a nested document with a CSS selector.

**Status: in progress; 11.0 is built, streams A and B are next.** Chosen by the owner on
2026-09-24, from their questions about `set v <thing a=1/>` and nested XML, and run as
[running.md](running.md) describes. Their third question, whether `ls | set files`
should show the table, they answered: it stays as it is.

## Requests

| # | The owner asked | Where |
| --- | --- | --- |
| T1 | A way to read a tag's name, `thing` in `<thing a=1/>` | A |
| T2 | A general way to extract anything from nested tags with different names and attributes, and to query it efficiently, borrowing from the web | B |

## Decision records

Written in 11.0, both Accepted: the owner's choices of 2026-09-24.

- [0048](../decisions/0048-a-tags-own-parts-are-read-with-at.md). `$v.@tag` is a tag's
  name and `$v.@children` its children as a list. A row answers its columns first, so a
  row with an `@tag` column reads that. Any other `@` member, or one on a value that is
  not a tag, is `None`.
- [0049](../decisions/0049-pick-selects-elements-with-css-selectors.md). `pick
  <selector>` answers a table of every element a CSS selector matches, the root
  included, in document order: `@tag`, the attributes in first-seen order, `@children`.
  The subset: type, `*`, `[a]`, `[a=v]`, `[a^=v]`, `[a$=v]`, `[a*=v]`, compounds,
  descendant, child, groups. A table whose rows have `@tag` is read back as elements.

## Checkpoint 11.0: foundation

The orchestrator: the records and their index rows; `Takes.Selector` in
`Core/Commands/Spec.fs`, described in `help` and offering nothing yet in completion;
`Core/Selector.fs` registered after `Table.fs`, with the subset named; and
`Core.Tests/SelectorTests.fs` registered. **Done when** every suite passes and the browser
check passes.

## Parallel streams

After 11.0, A and B start together from its commit.

### A. `@` members

**Closes** T1. **Size** S to M.

- The grammar: in `Parser.FParsec/Grammar.fs`, `memberName` also takes `.@` followed by
  an identifier, naming the member `@name`. Only there; the GOLD parser is not touched.
  The tokens the page colours come from the tree, so check `$v.@tag` is drawn as a
  variable and its member as it draws `$v.a`.
- `Expr.readMember`: on a tag, `@tag` and `@children` read an attribute of that name
  when there is one (a row), and otherwise the tag's name as text and its children as a
  list. Every other `@` name, and either on any other value, is `None`.
- Completion: after `$v.` on a variable holding a tag, `@tag` and `@children` are offered
  after the attributes, with details `the tag's name` and `its children`; typing `$v.@`
  offers them alone. Hover on `@tag` says what it reads.
- **Tests:** FParsec-only parser cases in `Parser.Tests/BareWordTests.cs` or a new class
  there (`$v.@tag`, `$v.@children`, `$row.@tag eq book`, `echo user@host` still a word);
  `ExpressionTests.fs` for `readMember`; `VariableCompletionTests.fs`; one line through
  the session: `set v <thing a=1/>` then `echo $v.@tag` answers `thing`.
- **Owns** `memberName` in `Parser.FParsec/Grammar.fs`, `readMember` in `Core/Expr.fs`,
  member completion in `Core/Completion/Variables.fs` and `Core/Completion/Hover.fs`.
  **Touches** `Parser.Tests/` (new cases only), `Core.Tests/ExpressionTests.fs`,
  `Core.Tests/VariableCompletionTests.fs`, `Core.Tests/HoverTests.fs`.

### B. `pick`

**Closes** T2. **Size** M.

- `Core/Selector.fs`: parse a selector into a value (fault on anything outside the
  subset, saying where it stopped, kind `Syntax`), and match it over a tag's tree: every
  element, the root included, in document order, each once however many groups match.
  Attribute values compare as their display text, exactly; values in a selector may be
  bare or quoted.
- `pick`, read-only, in `Core/Commands/Documents.fs`, with keywords such as `select`,
  `query`, `css`, `find`, `xpath`: a required `selector` (`Takes.Selector`) and the piped
  value, which is a tag, a list of tags, or a table whose rows have `@tag` (each row read
  back as its element: `@tag` its name, `@children` its children, the other columns its
  attributes, gaps left out). Anything else is a fault naming what it was.
- The answer is the table of 0049. No match is the empty table with the column `@tag`
  and `@children` only.
- Completion for `Takes.Selector` in `Core/Completion/Arguments.fs`: the distinct element
  names of the value flowing in, in document order, when the word being written is a
  plain name; nothing inside `[` or after an operator.
- **Tests** in `SelectorTests.fs`: parsing, each part of the subset, groups and order,
  the root included, a missing attribute, quoted values, faults with position, the table
  shape, `pick` on `from-xml` of a nested file, picking from a pick, and the lines of
  [Acceptance](#acceptance) that do not need A. Completion in
  `ArgumentCompletionTests.fs`.
- **Owns** `Core/Selector.fs`, `pick` in `Core/Commands/Documents.fs`,
  `Takes.Selector` in `Core/Completion/Arguments.fs`, `Core.Tests/SelectorTests.fs`.
  **Touches** the command list in `Core/Session.fs` (one line) and
  `Core.Tests/ArgumentCompletionTests.fs` (new tests only).

## The owner's answers

The streams' reports raised four questions, asked together and answered on 2026-09-24,
all with the option recommended:

- A bare word may start with `@`, so `select @tag` works, and a row answers any `@`
  column by name ([0050](../decisions/0050-at-names-are-words-and-columns.md)). The
  orchestrator: the grammar and `readMember`.
- A list in a table cell is summarised, `2 children` or `3 items`
  ([0051](../decisions/0051-a-list-in-a-table-cell-is-summarised.md)). The orchestrator:
  the core's table display.
- `pick` answers each element once when the documents given overlap
  ([0052](../decisions/0052-pick-answers-each-element-once.md)). Stream B, sent back,
  with completion after any upstream (0049), which needs `Shape` to keep the value it
  previewed, and `pick` taken out of `select`'s keywords.
- Children made into a table keep the type `row`, so `$row.@tag` there reads `row`;
  `pick` is the way to read elements as rows, and this stays as it is.

## Who touches what

| File | 11.0 | A | B | 11.9 |
| --- | --- | --- | --- | --- |
| `docs/decisions/` | own | | | index |
| `Core/Commands/Spec.fs`, `Core/Commands/Meta.fs` | `Takes.Selector` | | | |
| `Core/Core.fsproj`, `Core.Tests/Core.Tests.fsproj` | own | | | |
| `Parser.FParsec/Grammar.fs` | | `memberName` | | |
| `Core/Expr.fs` | | `readMember` | | |
| `Core/Completion/Variables.fs`, `Hover.fs` | | own | | |
| `Core/Completion/Arguments.fs` | stub | | `Takes.Selector` | |
| `Core/Selector.fs` | stub | | own | |
| `Core/Commands/Documents.fs` | | | `pick` | |
| `Core/Session.fs` | | | one line | |
| `Parser.Tests/`, `ExpressionTests.fs`, `VariableCompletionTests.fs`, `HoverTests.fs` | | own tests | | |
| `SelectorTests.fs`, `ArgumentCompletionTests.fs` | stub | | own tests | |
| `tools/browser-check.mjs` | | | | own |
| `docs/` user documents, `guide/`, `docs/spec/` | | | | own |

## Running it

1. **11.0: the orchestrator.**
2. **A and B: two sub-agents, launched together,** each in its own worktree from the
   11.0 commit. Merge order: A, then B, since B's `$row.@tag` lines read through A's
   grammar.
3. **11.9: integration.** The orchestrator runs the acceptance lines that need both,
   and adds them to the browser check. Then two sub-agents as in Phase 10, *docs* (the
   user documents and the guide: `@`, `pick`, a nested example from `from-xml`) and
   *spec* (the grammar, the member rules, `pick` in the command catalogue, conformance
   counts), then a verifier, the republish, "As built" and the status lines.

## Acceptance

| Line | Does |
| --- | --- |
| `set v <thing a=1/>` then `echo $v.@tag` | `thing` |
| `echo $v.a` | `1`, as before |
| `set d <library city=paris><book title=dune year=1965><author name=herbert/></book><book title=emma year=1815><author name=austen/></book><shelf/></library>` then `$d.@children | pick "*" | select @tag` | `book`, `author`, `book`, `author`, `shelf`: a list of tags is read as that many documents |
| `$d | pick book` | a table: `@tag`, `title`, `year`, `@children`; two rows, `dune` then `emma` |
| `$d | pick "book > author" | select name` | `herbert`, `austen` |
| `$d | pick "*" | select @tag` | `library`, `book`, `author`, `book`, `author`, `shelf` |
| `$d | pick "book[year^=19]" | select title` | `dune` |
| `$d | pick "shelf, author"` | three rows, in document order |
| `$d | pick book | where $row.year gt 1900 | select title` | `dune` |
| `$d | pick book | pick author | select name` | `herbert`, `austen` |
| `$d | pick "book >"` | a `Syntax` fault that says where it stopped |
| `$d | pick ` then Tab | the element names `library`, `book`, `author`, `shelf` offered |
| `$v.` in the input | `a`, `@tag`, `@children` offered |

## Progress

| Work | Done by | State | Commit |
| --- | --- | --- | --- |
| 11.0 Foundation | orchestrator | merged | 2e4b57f |
| A. `@` members | stream agent | merged | ec21f8a |
| B. `pick` | stream agent | merged | 7f8936f |
| Owner's answers (0050 to 0052) | orchestrator, stream B | merged | d96eabc, d519d77 |
| 11.9 acceptance in the browser check | orchestrator | merged | this commit |
| 11.9 docs, spec | two sub-agents | in progress | |
| 11.9 verifier, republish, As built | orchestrator | not started | |
