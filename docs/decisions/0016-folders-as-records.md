# 0016. Folders are records with a `kind` of `folder`, and the root is implicit

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-21 |

## Context

[0013](0013-attribute-filesystem.md) settled that a file is a record of typed
attributes and that `folder` is one ordinary attribute among them. It did not settle
what a folder itself is. The store holds records; a folder has to be either a record
like any other, a separate kind of entity, or nothing at all beyond the strings that
appear in other records' `folder` attributes.

The question matters because `mkdir` has to emit an event, `rm` has to be able to
delete a folder, and `ls /documents` has to be able to fail when `/documents` does not
exist rather than quietly listing nothing.

## Options

1. **Folders are implied by paths.** A folder exists when some record names it. `mkdir`
   has nothing to record, so an empty folder cannot exist and `mkdir a` followed by
   `ls` shows nothing. Undoing `mkdir` is a no-op.
2. **Folders are a separate table in the projection.** Two record types, two sets of
   events, two resolution paths. Every query over attributes has to decide whether it
   covers folders.
3. **Folders are records with `kind = folder`.** One record type, one set of events.
   A folder has a `name`, a `folder` (its parent's path), `created` and `modified`
   like anything else, and may carry user attributes too. It has no content.

## Decision

Option 3. `mkdir` emits `FileCreated` of a record whose `kind` is `folder`; `rm`
emits `FileDeleted` of the same record, so undo restores it with its attributes
intact. A folder's path is its parent's path plus `/` plus its name.

The root is implicit: it is the path `/`, it is not a record, and it cannot be
deleted, renamed or given attributes. Making it a record would mean a record whose
`folder` attribute has no meaningful value, and a `name` that never appears in any
path.

Names are unique within a folder across files and folders together, so `mkdir notes`
in a folder that already holds `notes` is a `Conflict`.

## Consequences

One resolution function serves both, and a query over attributes sees folders unless
it excludes them — which is what makes `$row.kind eq folder` a useful predicate later.
`ls` can order folders before files because it can see which is which. The root
needing a special case in `pathOf` and in `rm` is the price; it is two lines.
