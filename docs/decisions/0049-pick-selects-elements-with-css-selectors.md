# 0049. `pick` selects elements with CSS selectors

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

A tag is a table when its children share a type and have none of their own (decision
0009), and then `where`, `select` and `sort` work on it. Anything deeper is a dead end:
`<library>` holding `<book>`s holding `<author>`s is `not a table`, and nothing reaches
inside it, whether it was typed or read with `from-xml`.

## Options

1. **Flatten the tree into a table** of every element, and filter that with `where`.
2. **XPath.** Powerful, with axes and positions, and heavy to type on a phone.
3. **CSS selectors**, which pick elements by their shape in a line everyone who has
   written a stylesheet can read, and leave comparing values to `where`.

## Decision

Option 3, chosen by the owner. `pick <selector>` reads the tag piped in, or each of a
list of tags, as a document whose root is that tag, and answers a table of every
element the selector matches, the root included, in document order, each once.

The selector is a subset of CSS: a type (`book`), `*`, attribute tests (`[year]`,
`[year=1965]`, `[title^=du]`, `[title$=ne]`, `[title*=un]`), several of them together
(`book[year]`), the descendant combinator (a space), the child combinator (`>`), and
groups joined by commas. Names and values are compared exactly, as XML compares them. A
selector outside the subset is a fault that says where it stopped.

The table has a column `@tag`, the element's name, first; then the attributes of the
elements matched, in the order they first appear, a gap where an element lacks one; then
`@children`, the element's children as a list. So `$row.@tag` and `$row.@children` read
as they do on the element itself (decision 0048), and
`$d | pick book | where $row.year gt 1900 | select title` is the whole of a query.

A table whose rows have `@tag` is read back as its elements, so `pick` can be applied
to what `pick` answered.

## Consequences

Selectors pick by shape and `where` filters by value; CSS's attribute tests are string
tests only, and comparisons stay in the predicate language. `Takes.Selector` lets
completion offer the element names of the document flowing in. A document in a tab has
at most thousands of elements, and a match is one walk of them; nothing is indexed.
