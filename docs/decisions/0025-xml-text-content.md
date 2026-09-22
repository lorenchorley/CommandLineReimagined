# 0025. XML element text content is read as a `text` attribute

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-22 |

## Context

Phase 6 reads XML files into the same tree the tag notation produces
([0011](0011-real-xml-files.md)): an element name, attributes, and child elements. That
tree has nowhere for text. `<note mood=good/>` is a tag; `<note>Stand-up moved to
ten.</note>` is an element whose only content is a sentence, and the notation has no
way to write it. Decision 0011 put text content out of scope for the first release and
asked for the limitation to be recorded when it arrived. It has arrived: a reader that
meets text has to do something with it, and "fail" or "drop it" are both somethings.

## Options

1. **Refuse a document with text content.** Honest, and it makes `from-xml` useless on
   most XML anyone already has, where the data is in the text rather than the attributes.
2. **Drop the text.** Nothing breaks and something silently goes missing, which is the
   one kind of loss a user cannot see to complain about.
3. **Give the tree a text node.** A new `Value` case or a `Text` field on `Tag`, and a
   notation to write it in, which is a grammar change and a table-coercion change for a
   case nobody has a program for yet.
4. **Read the text as an attribute called `text`, and write that attribute back as
   text.** An element's text becomes one more attribute, so it shows up in a table as a
   column, a predicate can read `$row.text`, and nothing else in the language changes.

## Decision

Option 4. It is the smallest thing that loses nothing in the common cases, and it is
exactly as far as the tree can go without a new notation.

The rules, which are the first release's and are expected to be replaced when text
content is taken on properly:

- Reading: the element's own text nodes that are not only whitespace are trimmed and
  joined with one space into an attribute `text`, typed by the same number-or-text rule
  as every other attribute. If the element also has an XML attribute named `text`, the
  content wins. Whitespace-only text is formatting, and is dropped.
- Writing: a tag attribute named `text` is written as the element's content, before any
  children, instead of as an XML attribute.

## Consequences

A round trip is lossless, at the level of the value, for elements with only attributes,
elements with only text, and elements with both. It is lossy for:

- **Mixed content**, where text and child elements interleave: the text is gathered into
  one attribute and written back before the children, so `<p>a<b/>c</p>` returns as
  `<p>a c<b/></p>`.
- **Whitespace** at either end of an element's text, which is trimmed.
- **An XML attribute called `text`** on an element that also has text content, which the
  content replaces.
- **A tag attribute called `text`** that was meant as an attribute: `to-xml` writes it as
  content. It reads back as the same value, so only someone reading the file sees the
  difference.

Comments and processing instructions are dropped, as decision 0011 already implied by
reading into a tree that cannot hold them. The conformance document lists all of this
under known deviations, so none of it is a surprise to anyone who looks.
