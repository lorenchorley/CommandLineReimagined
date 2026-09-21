# 0011. XML is read and written as real files

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-21 |

## Context

Tables come from the tag notation ([0009](0009-table-coercion.md)). The question was
whether XML support stops at the notation or extends to documents on the filesystem.

## Options

1. **Notation only.** Tags in the command line become tables; files are out of scope.
2. **Real documents too.** `from-xml` parses a file's text into the same tree the tag
   notation produces, and `to-xml` writes a tree or a table back as a document.

## Decision

Option 2. The tag grammar and the XML reader produce the same tree type, so one
coercion rule serves both.

## Consequences

XML documents that are not table-shaped are still readable as trees. Attributes only
in the first version: element text content and mixed content are recorded as a
limitation when they arrive.
