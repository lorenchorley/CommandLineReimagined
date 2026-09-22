# 0027. `save` takes its attributes in the tag, not as assignments

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-22 |
| Supersedes | One sentence of [0017](0017-assignment-arguments.md)'s consequences |

## Context

[Decision 0017](0017-assignment-arguments.md) made `name=value` in argument position an
assignment, collected by a command that declares an `Assignments` parameter. Its
consequences say "`attr` and `save` take any attribute name without quoting or a tag."
`attr` was built that way. `save` was not: it takes one tag, and its record is the tag's
type as the kind and the tag's attributes as the attributes, so `save note name=x`
answers `'save' does not take 'name=' assignments.`

Phase 7 reconciled the specification with the code and found the two disagreeing. One
of them has to change, and the choice is a language question, so it is recorded.

## Options

1. **`save` takes a kind word and assignments.** `save note name=monday mood=good`
   makes the same record as `save <note name=monday mood=good/>`. No angle brackets,
   which are awkward on a phone keyboard. Two spellings for one record, and `save`'s
   first argument becomes either a tag or a word depending on what was typed, so a
   piped tag and a written kind compete for the same parameter.
2. **`save` takes a tag and assignments that override it.**
   `save <note/> name=monday` merges. It still needs a tag, so it does not deliver what
   the sentence promised, and it gives every attribute two places to be written.
3. **`save` takes a tag, as built, and the sentence is withdrawn.** A tag with
   attributes is exactly a file record ([0013](0013-attribute-filesystem.md)), so the
   tag is already the notation for "a record with these attributes". Assignments are for
   changing a record that exists, which is `attr`.

## Decision

Option 3. `save` takes one tag, which may be piped. Its type is the new record's kind,
its `name` attribute is the name, and every other attribute is kept as written.
Assignments on a `save` line are refused with the binder's ordinary message.

## Consequences

Making a record and changing one are spelled differently, and each has one spelling:
a tag to make it, `attr` with assignments to change it. A tag built earlier in the line
or held in a variable saves the same way as one written out, which option 1 would have
had to special-case.

The phone-keyboard cost of option 1's argument stands. If it is taken up later, it is a
new record: a kind word followed by assignments is a notation for a record, and it
should then work wherever a tag does, not in `save` alone.
