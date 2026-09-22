# Phase 6: XML and CSV as real files

**Goal.** `from-xml`, `to-xml`, `from-csv`, `to-csv` read and write files in the store.
Decision [0011](../decisions/0011-real-xml-files.md).

**Record to add first.** [0025](../decisions/0025-xml-text-content.md): element text
content is out of scope for this release; elements with text content are read with a
`text` attribute holding it, and written back the same way, so a round trip is lossless
for attribute-only and text-only elements and documented as lossy for mixed content.
(This document first numbered it 0020, which Phase 3 took for scripts; records are
numbered in the order they are taken.)

## Semantics

- `from-xml path` parses the file's content with `System.Xml.Linq` into `Value.Object`
  using the same `Tag` shape the grammar produces: element name to `TypeName`,
  attributes to `Attributes` (typed by the same number-or-text rule as the grammar),
  child elements to `Children`. A document whose root is table-shaped coerces to a
  table wherever a table is expected, exactly as a tag does. Namespaces are kept as
  written in the name. Comments and processing instructions are dropped.
- `to-xml value path` writes a `Tag`, a `Table` (via `Table.toTag`, root element
  `table`, rows `row` unless a `-root` and `-row` flag name them) or a `List` of
  objects. Pretty-printed, UTF-8, no declaration unless `-declaration`. Returns the
  `File` written; `value` is piped.
- `from-csv path` reads RFC 4180 with a header row into a `Table`; all columns text
  unless every cell of a column parses as a number. `-delimiter` flag.
- `to-csv table path` writes with a header row, quoting only when needed, every line
  ending in `\n` including the last. Returns the `File` written; `table` is piped.
- Kind inference: `.xml` gives `xml`, `.csv` gives `csv`; `to-xml`/`to-csv` set the
  kind on the files they create.

Both writers emit the same events `write` does, so undo and history work unchanged.

## Tests

- Core: XML round trip for a table-shaped document and for a nested one; a document
  with text content gains `text`; malformed XML is an `Invalid` fault naming the line;
  CSV round trip including quoted commas and newlines; a table with `None` cells writes
  empty fields and reads back as `None`.
- Browser check: `run examples/inventory.clr` completes and the last entry shows the
  bolts row; `ls | to-xml listing.xml` then `from-xml listing.xml | count` equals the
  row count.
- `ExampleProgramTests`: every golden result of `inventory.clr`, including the exact
  CSV text.

## Documentation

`docs/tables.md` gains "Reading and writing files"; `docs/commands.md`; command
catalogue; conformance known deviations gains the text-content limitation.
