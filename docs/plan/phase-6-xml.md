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

## As built

Built as written, with these differences, recorded so the next phase starts from what
is true:

- **A second record.** [0026](../decisions/0026-inventory-sorts-its-reorder-list.md):
  `inventory.clr`'s golden reorder file lists C3 before B2, and the line that writes it
  never sorted, so no implementation of the rules already decided could produce it. The
  line gained `sort qty`; every golden result stands as written.
- **`to-xml path value`, not `to-xml value path`.** The path comes first and the value
  is piped, which is the shape `write` has, so `... | to-csv reorder.csv` reads the way
  it looks. The name options come after both. `-declaration` is a switch, and a flag
  followed by a plain word takes the word as its value, so it is written after the path.
- **`-delimiter` on the writer too**, and `tab` accepted by name, since nobody can type a
  tab on a phone. `-row` names a table's rows; a list is written under a root called
  `list`, each item keeping its own name.
- **Empty text is quoted in CSV.** An unquoted empty field is a gap and `""` is empty
  text, so both survive a round trip as the different things they are. The empty file is
  the empty table.
- **Numbers go into files exactly.** `display` rounds to three places for a person;
  the writers use the shortest exact form, or writing a table out and reading it back
  would change it.
- **The XML fault leaves the parser's sentence out.** It names the file, the line and
  the position. The parser's own wording is a resource the browser build does not carry,
  and a message that reads differently on two hosts cannot be tested on either. The
  browser check asserts the message as the page shows it.
- **A DTD is refused**, so a document cannot expand an entity into a stopped tab.
- **Namespaces are kept as written**: a prefixed name is its text, and the declarations
  are attributes, which is what lets a document written back out mean what it did.
- **The XML writer is by hand.** `XmlWriter` will not write a prefixed name without the
  namespace behind it, and the tree only knows the prefix, so names are checked against
  XML's rules instead.
- **The example matcher ignores the line break a file ends in.** A golden result is the
  lines a person sees, and `cat` on a CSV answers text ending in one; the exact CSV text
  is pinned by a test of its own.

The command line cannot type a double quote inside a string, so the CSV reader's quoting
rules are tested against the `Csv` module directly, and through a session with seeded
files. `System.Xml.Linq` was already in the payload; the published client is 9.9 MB
once the precompressed copies are set aside, which is how the CI guard measures it.
