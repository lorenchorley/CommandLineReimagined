# 0013. An attribute-and-query filesystem in the style of BeOS and Haiku

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-21 |

## Context

The owner asked whether the filesystem should put less emphasis on tree structure and
more on categorisation, as the Be File System did: files carry typed attributes,
attributes are indexed, and a query over them is a first-class object that behaves
like a folder. The browser sandbox has no legacy tree to honour, and the language
already treats structured values as its native material.

## Options

1. **Hierarchical, as now.** Paths, directories, `cd` and `up`. Familiar, and a poor
   fit for a table-centred language: `ls` is the only view.
2. **Attributes, queries as folders, no hierarchy at all.** Every file is a record of
   attributes; `ls` is a query; there is no `cd`. Maximal purity; unfamiliar, and
   importing real files has nowhere to put their paths.
3. **Attributes first, with a folder attribute kept.** Files are attribute records
   with a `kind`, a `name` and any number of typed attributes. A query is the primary
   way to find things and a saved query is a view. `folder` is one ordinary attribute,
   so trees remain expressible and imports have a home. `cd` sets the current view,
   which may be a folder or any query, and `pwd` shows it.

## Decision

Option 3. It keeps every existing command meaningful, gives the table
functions a natural target, and unifies the tag notation with the file model: a tag
with attributes is exactly a file record.

## Consequences

The store from [0010](0010-undo-by-event-sourcing.md) holds attribute records and
content blobs rather than a directory tree. `ls` returns a table whose columns are the
attributes present. Queries reuse the predicate syntax from
[0007](0007-notation-conflicts.md) and [0008](0008-explicit-row-variable.md). Live
views, which update as the store changes, are a later step the event log makes cheap.
