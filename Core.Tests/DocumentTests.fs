namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>XML and CSV as real files (Phase 6).</summary>
/// <remarks>
/// Decision 0011 and decision 0025. The readers and writers are pure, so most of what
/// they promise is pinned against the modules directly, where a document can hold a
/// quote the command line cannot type; the rest is pinned through a session, because
/// what matters there is that a document is a file like any other — undone like one,
/// listed like one, and read back into the value it was written from.
/// </remarks>
[<TestClass>]
type XmlTests() =

    let read (content: string) = Xml.read "/test.xml" content |> expectOk

    let write (value: Value) = Xml.write Xml.defaults value |> expectOk

    let tag name attributes children = Tag.create name attributes children

    let items =
        tag
            "items"
            []
            [ Value.Object(tag "item" [ "sku", text "A1"; "qty", Value.Number 120.0 ] [])
              Value.Object(tag "item" [ "sku", text "B2"; "qty", Value.Number 12.0 ] []) ]

    // ---------------------------------------------------------------- reading

    [<TestMethod>]
    member _.AnElementIsATag() =
        let read = read "<item sku=\"A1\" qty=\"120\"/>"

        Assert.AreEqual<string>("item", read.TypeName)
        Assert.AreEqual<Value>(text "A1", read.Attributes["sku"])
        Assert.AreEqual<Value>(Value.Number 120.0, read.Attributes["qty"])

    /// The same number-or-text rule a bare word in a tag gets, so a document and the
    /// notation it was written from read as the same value.
    [<TestMethod>]
    member _.AttributesAreTypedTheWayTheNotationTypesThem() =
        let harness = seeded ()
        let written = harness.Run "<item sku=A1 qty=120 price=2.5/>"
        harness.Run "<item sku=A1 qty=120 price=2.5/> | to-xml item.xml" |> ignore

        Assert.AreEqual<Value>(written, harness.Run "from-xml item.xml")

    [<TestMethod>]
    member _.AttributesKeepTheOrderTheDocumentHas() =
        let read = read "<item z=\"1\" a=\"2\" m=\"3\"/>"

        Assert.AreEqual<string list>([ "z"; "a"; "m" ], Value.orderedAttributes read |> List.map fst)

    [<TestMethod>]
    member _.ChildElementsAreChildren() =
        let read = read "<a><b/><c><d/></c></a>"

        Assert.AreEqual<string>("<a><b/><c><d/></c></a>", Value.display (Value.Object read))

    /// Decision 0025.
    [<TestMethod>]
    member _.TextContentIsReadAsATextAttribute() =
        let read = read "<note mood=\"good\">Stand-up moved to ten.</note>"

        Assert.AreEqual<Value>(text "Stand-up moved to ten.", read.Attributes[Xml.textAttribute])
        Assert.AreEqual<Value>(text "good", read.Attributes["mood"])

    [<TestMethod>]
    member _.TextContentIsTypedLikeAnAttribute() =
        Assert.AreEqual<Value>(Value.Number 42.0, (read "<qty>42</qty>").Attributes[Xml.textAttribute])

    /// The indentation of a pretty-printed document is not something it says.
    [<TestMethod>]
    member _.WhitespaceBetweenElementsIsNotText() =
        let read = read "<a>\n  <b/>\n  <c/>\n</a>"

        Assert.IsFalse(read.Attributes.ContainsKey Xml.textAttribute)
        Assert.AreEqual<int>(2, List.length read.Children)

    /// Decision 0025's lossy case: the runs are trimmed and joined.
    [<TestMethod>]
    member _.MixedContentIsGatheredIntoOneAttribute() =
        let read = read "<p> a <b/> c </p>"

        Assert.AreEqual<Value>(text "a c", read.Attributes[Xml.textAttribute])
        Assert.AreEqual<int>(1, List.length read.Children)

    [<TestMethod>]
    member _.ContentWinsOverAnAttributeCalledText() =
        let read = read "<a text=\"attribute\">content</a>"

        Assert.AreEqual<Value>(text "content", read.Attributes[Xml.textAttribute])

    [<TestMethod>]
    member _.CdataIsText() =
        Assert.AreEqual<Value>(text "a < b", (read "<a><![CDATA[a < b]]></a>").Attributes[Xml.textAttribute])

    [<TestMethod>]
    member _.CommentsAndProcessingInstructionsAreDropped() =
        let read = read "<?xml version=\"1.0\"?><!-- a comment --><a><?pi data?><!-- another --><b/></a>"

        Assert.AreEqual<string>("<a><b/></a>", Value.display (Value.Object read))

    /// The tree has no namespaces, so names are kept as they were written and the
    /// declarations as the attributes they were, which is what writing back needs.
    [<TestMethod>]
    member _.NamespacesAreKeptAsWritten() =
        let document = "<x:a xmlns:x=\"urn:x\" xmlns=\"urn:d\"><x:b x:id=\"1\"/><c/></x:a>"
        let read = read document

        Assert.AreEqual<string>("x:a", read.TypeName)
        Assert.AreEqual<Value>(text "urn:x", read.Attributes["xmlns:x"])
        Assert.AreEqual<Value>(text "urn:d", read.Attributes["xmlns"])

        Assert.AreEqual<string>(
            "<x:a xmlns:x=urn:x xmlns=urn:d><x:b x:id=1/><c/></x:a>",
            Value.display (Value.Object read))

        Assert.AreEqual<Tag>(read, (Xml.read "/again.xml" (write (Value.Object read)) |> expectOk))

    // ---------------------------------------------------------------- faults

    [<TestMethod>]
    member _.MalformedXmlIsInvalidAndNamesTheLine() =
        let fault = Xml.read "/stock/items.xml" "<items>\n  <item>\n</items>" |> expectFault Invalid

        StringAssert.StartsWith(fault.Message, "Not well-formed XML : /stock/items.xml line 3,")
        Assert.AreEqual<string option>(Some "/stock/items.xml", fault.Path)

    [<TestMethod>]
    member _.AnEmptyFileIsNotADocument() =
        let fault = Xml.read "/empty.xml" "" |> expectFault Invalid

        StringAssert.StartsWith(fault.Message, "Not well-formed XML : /empty.xml line 1,")

    /// A file in the store is anyone's, and an entity that expands into itself is a way
    /// to stop a tab.
    [<TestMethod>]
    member _.ADocumentTypeIsRefused() =
        let bomb =
            "<!DOCTYPE a [<!ENTITY b \"bb\"><!ENTITY c \"&b;&b;&b;\">]><a>&c;</a>"

        Xml.read "/bomb.xml" bomb |> expectFault Invalid |> ignore

    // ---------------------------------------------------------------- writing

    [<TestMethod>]
    member _.ATagIsWrittenPrettyPrinted() =
        Assert.AreEqual<string>(
            "<items>\n  <item sku=\"A1\" qty=\"120\"/>\n  <item sku=\"B2\" qty=\"12\"/>\n</items>\n",
            write (Value.Object items))

    [<TestMethod>]
    member _.ATableIsWrittenAsRowsUnderATableRoot() =
        let table = Table.ofTag items |> expectOk

        Assert.AreEqual<string>(
            "<table>\n  <row sku=\"A1\" qty=\"120\"/>\n  <row sku=\"B2\" qty=\"12\"/>\n</table>\n",
            write (Value.Table table))

    [<TestMethod>]
    member _.RootAndRowNameWhatATableIsWrittenAs() =
        let table = Table.ofTag items |> expectOk

        Assert.AreEqual<string>(
            "<stock>\n  <item sku=\"A1\" qty=\"120\"/>\n  <item sku=\"B2\" qty=\"12\"/>\n</stock>\n",
            Xml.write { Xml.defaults with Root = Some "stock"; Row = Some "item" } (Value.Table table) |> expectOk)

    [<TestMethod>]
    member _.AListOfTagsIsWrittenUnderAListRoot() =
        Assert.AreEqual<string>(
            "<list>\n  <a/>\n  <b/>\n</list>\n",
            write (Value.List [ Value.Object(tag "a" [] []); Value.Object(tag "b" [] []) ]))

    [<TestMethod>]
    member _.TheDeclarationIsOnlyWrittenWhenAskedFor() =
        let value = Value.Object(tag "a" [] [])

        Assert.AreEqual<string>("<a/>\n", write value)

        Assert.AreEqual<string>(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<a/>\n",
            Xml.write { Xml.defaults with Declaration = true } value |> expectOk)

    /// A gap is an attribute that is not there, the same as it is in a row.
    [<TestMethod>]
    member _.AGapIsNotWritten() =
        Assert.AreEqual<string>("<a b=\"1\"/>\n", write (Value.Object(tag "a" [ "b", Value.Number 1.0; "c", Value.None ] [])))

    [<TestMethod>]
    member _.TheTextAttributeIsWrittenAsContent() =
        Assert.AreEqual<string>(
            "<note mood=\"good\">Stand-up moved to ten.</note>\n",
            write (Value.Object(tag "note" [ "mood", text "good"; "text", text "Stand-up moved to ten." ] [])))

    [<TestMethod>]
    member _.WhatXmlReservesIsEscaped() =
        let value = Value.Object(tag "a" [ "q", text "say \"<&>\"\nnext"; "text", text "1 < 2 & 3 > 2" ] [])
        let written = write value

        Assert.AreEqual<string>("<a q=\"say &quot;&lt;&amp;&gt;&quot;&#xA;next\">1 &lt; 2 &amp; 3 &gt; 2</a>\n", written)
        Assert.AreEqual<Value>(value, Value.Object(Xml.read "/a.xml" written |> expectOk))

    /// A number is written with every digit it has: `display` would round it to three
    /// places, and reading the file back would give a different table.
    [<TestMethod>]
    member _.NumbersAreWrittenExactly() =
        Assert.AreEqual<string>("<a pi=\"3.14159\"/>\n", write (Value.Object(tag "a" [ "pi", Value.Number 3.14159 ] [])))

    [<TestMethod>]
    member _.ANameXmlDoesNotAllowIsInvalid() =
        let table = Table.ofColumns [ "first name" ] [ [ text "Ada" ] ]
        let fault = Xml.write Xml.defaults (Value.Table table) |> expectFault Invalid

        Assert.AreEqual<string>("'first name' is not a name XML allows.", fault.Message)

    [<TestMethod>]
    member _.OnlyTagsTablesAndListsOfTagsAreDocuments() =
        let fault = Xml.write Xml.defaults (text "hello") |> expectFault Binding
        Assert.AreEqual<string>("'to-xml' needs a tag, a table or a list of tags, not text.", fault.Message)

        Xml.write Xml.defaults (Value.List [ text "hello" ]) |> expectFault Binding |> ignore

    // ------------------------------------------------------------- round trips

    [<TestMethod>]
    member _.ATableShapedDocumentRoundTrips() =
        let value = Value.Object items

        Assert.AreEqual<Value>(value, Value.Object(Xml.read "/items.xml" (write value) |> expectOk))

    [<TestMethod>]
    member _.ANestedDocumentRoundTrips() =
        let value =
            Value.Object(
                tag
                    "library"
                    [ "name", text "home" ]
                    [ Value.Object(
                          tag
                              "shelf"
                              [ "room", text "study" ]
                              [ Value.Object(tag "book" [ "title", text "Dune"; "text", text "A desert planet." ] [])
                                Value.Object(tag "book" [ "title", text "Emma" ] []) ]
                      )
                      Value.Object(tag "shelf" [ "room", text "hall" ] []) ]
            )

        Assert.AreEqual<Value>(value, Value.Object(Xml.read "/library.xml" (write value) |> expectOk))

    /// Decision 0025: text and children both come back, the text written first.
    [<TestMethod>]
    member _.AnElementWithTextAndChildrenRoundTrips() =
        let value = Value.Object(tag "p" [ "text", text "intro" ] [ Value.Object(tag "b" [] []) ])

        Assert.AreEqual<string>("<p>\n  intro\n  <b/>\n</p>\n", write value)
        Assert.AreEqual<Value>(value, Value.Object(Xml.read "/p.xml" (write value) |> expectOk))

    // ------------------------------------------------------------ as commands

    [<TestMethod>]
    member _.ToXmlAnswersTheFileItWrote() =
        let harness = seeded ()

        match harness.Run "<a b=1/> | to-xml a.xml" with
        | Value.File file ->
            Assert.AreEqual<string>("a.xml", file.Name)
            Assert.AreEqual<string>("xml", file.Kind)
        | other -> Assert.Fail(sprintf "Expected a file, got %s." (Value.kind other))

        Assert.AreEqual<string>("<a b=\"1\"/>\n", harness.Content "/a.xml")

    /// The kind is the writer's, not the extension's: what is in the file is XML.
    [<TestMethod>]
    member _.ToXmlSetsTheKindOfAFileItCreates() =
        let harness = seeded ()
        harness.Run "<a/> | to-xml exported" |> ignore

        Assert.AreEqual<string>("xml", harness.Attribute "/exported" "kind")

    [<TestMethod>]
    member _.ToXmlTakesItsNamesFromFlags() =
        let harness = seeded ()
        harness.Run "ls | select name | take 1 | to-xml out.xml -root listing -row entry -declaration" |> ignore

        Assert.AreEqual<string>(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<listing>\n  <entry name=\"documents\"/>\n</listing>\n",
            harness.Content "/out.xml")

    [<TestMethod>]
    member _.ToXmlOverwritesAndUndoRestores() =
        let harness = seeded ()
        harness.Run "<a/> | to-xml a.xml" |> ignore
        harness.Run "<b/> | to-xml a.xml" |> ignore

        Assert.AreEqual<string>("<b/>\n", harness.Content "/a.xml")
        harness.Run "undo" |> ignore
        Assert.AreEqual<string>("<a/>\n", harness.Content "/a.xml")
        harness.Run "undo" |> ignore
        Assert.IsFalse(harness.Exists "/a.xml")

    /// Decision 0009 for documents: a table-shaped root is a table wherever one is
    /// expected, with nothing in between.
    [<TestMethod>]
    member _.FromXmlIsATableWhereATableIsExpected() =
        let harness = seeded ()
        harness.Run "<items><item sku=A1 qty=120/><item sku=B2 qty=12/></items> | to-xml items.xml" |> ignore

        Assert.AreEqual<string list>([ "B2"; "A1" ], harness.Column "from-xml items.xml | sort qty" "sku")

    [<TestMethod>]
    member _.FromXmlChangesNothing() =
        let harness = seeded ()
        harness.Run "<a/> | to-xml a.xml" |> ignore
        let before = harness.History()
        harness.Run "from-xml a.xml" |> ignore

        Assert.AreEqual<string list>(before, harness.History())

    [<TestMethod>]
    member _.FromXmlTakesAPipedFile() =
        let harness = seeded ()
        harness.Run "<a b=1/> | to-xml a.xml" |> ignore

        Assert.AreEqual<string>("<a b=1/>", harness.Text "to-xml a.xml <a b=1/> | from-xml")

    [<TestMethod>]
    member _.FromXmlOnAMissingFileIsNotFound() =
        let harness = seeded ()

        Assert.AreEqual<string>("File does not exist : /missing.xml", harness.Error "from-xml missing.xml")

    [<TestMethod>]
    member _.FromXmlOnAFolderIsInvalid() =
        let harness = seeded ()

        Assert.AreEqual<string>("That is a directory, not a file : /documents", harness.Error "from-xml documents")

    /// Phase 6's browser check, as a test: a listing written out reads back as as many
    /// rows as it had.
    [<TestMethod>]
    member _.AListingWrittenAsXmlReadsBackWithItsRowCount() =
        let harness = seeded ()
        let rows = harness.Table "ls" |> fun table -> List.length table.Rows
        harness.Run "ls | to-xml listing.xml" |> ignore

        Assert.AreEqual<string>(string rows, harness.Text "from-xml listing.xml | count")

    [<TestMethod>]
    member _.AFailedWriteLeavesNothing() =
        let harness = seeded ()
        harness.Fail "echo hello | to-xml a.xml" |> ignore

        Assert.IsFalse(harness.Exists "/a.xml")

[<TestClass>]
type CsvTests() =

    let read (content: string) = Csv.read "/test.csv" ',' content |> expectOk

    let write (table: Table) = Csv.write ',' table

    let cells (table: Table) = table.Rows |> List.map (List.map Value.display)

    // ---------------------------------------------------------------- reading

    [<TestMethod>]
    member _.TheHeaderNamesTheColumns() =
        let table = read "sku,qty\nC3,0\nB2,12\n"

        Assert.AreEqual<string list>([ "sku"; "qty" ], Table.names table)
        Assert.AreEqual<string list list>([ [ "C3"; "0" ]; [ "B2"; "12" ] ], cells table)

    /// Every cell of `qty` reads as a number, so the column is one; `sku` has a cell
    /// that does not, so every cell in it stays text, including the one that looks
    /// like a number.
    [<TestMethod>]
    member _.AColumnIsNumbersOnlyWhenEveryCellIsOne() =
        let table = read "sku,qty\n7,0\nB2,12\n"

        Assert.AreEqual<ColumnType list>([ TextCol; NumberCol ], table.Columns |> List.map (fun c -> c.Type))
        Assert.AreEqual<Value>(text "7", List.head (List.head table.Rows))
        Assert.AreEqual<Value>(Value.Number 12.0, List.item 1 (List.item 1 table.Rows))

    [<TestMethod>]
    member _.AnEmptyFieldIsAGap() =
        let table = read "a,b\n1,\n,2\n"

        Assert.AreEqual<Value list list>([ [ Value.Number 1.0; Value.None ]; [ Value.None; Value.Number 2.0 ] ], table.Rows)
        Assert.AreEqual<ColumnType list>([ NumberCol; NumberCol ], table.Columns |> List.map (fun c -> c.Type))

    [<TestMethod>]
    member _.QuotedEmptyTextIsNotAGap() =
        Assert.AreEqual<Value list list>([ [ text ""; text "x" ] ], (read "a,b\n\"\",x\n").Rows)

    [<TestMethod>]
    member _.QuotedFieldsHoldDelimitersQuotesAndLineBreaks() =
        let table = read "name,note\nbolts,\"a, b\"\nnuts,\"say \"\"hi\"\"\"\nwashers,\"one\ntwo\"\n"

        Assert.AreEqual<string list list>(
            [ [ "bolts"; "a, b" ]; [ "nuts"; "say \"hi\"" ]; [ "washers"; "one\ntwo" ] ],
            cells table)

    [<TestMethod>]
    member _.CarriageReturnLineEndsAreLineEnds() =
        Assert.AreEqual<string list list>([ [ "1"; "2" ] ], cells (read "a,b\r\n1,2\r\n"))

    [<TestMethod>]
    member _.TheLastLineNeedNotEnd() =
        Assert.AreEqual<string list list>([ [ "1"; "2" ] ], cells (read "a,b\n1,2"))

    [<TestMethod>]
    member _.ABlankLineInAWideFileIsSkipped() =
        Assert.AreEqual<string list list>([ [ "1"; "2" ]; [ "3"; "4" ] ], cells (read "a,b\n1,2\n\n3,4\n"))

    /// In a one-column file a blank line is a record with a gap in it, which is what
    /// writing a gap out produces.
    [<TestMethod>]
    member _.ABlankLineInANarrowFileIsAGap() =
        Assert.AreEqual<Value list list>([ [ text "x" ]; [ Value.None ] ], (read "a\nx\n\n").Rows)

    [<TestMethod>]
    member _.AnEmptyFileIsAnEmptyTable() =
        Assert.AreEqual<Table>(Table.empty, read "")

    [<TestMethod>]
    member _.ADelimiterCanBeChosen() =
        let table = Csv.read "/t.csv" ';' "a;b\n1,5;2\n" |> expectOk

        Assert.AreEqual<string list list>([ [ "1,5"; "2" ] ], cells table)

    [<TestMethod>]
    member _.TabIsADelimiterByName() =
        Assert.AreEqual<char>('\t', Csv.delimiter "tab" |> expectOk)
        Csv.delimiter ";;" |> expectFault Invalid |> ignore
        Csv.delimiter "\"" |> expectFault Invalid |> ignore

    // ---------------------------------------------------------------- faults

    [<TestMethod>]
    member _.ARecordOfTheWrongWidthNamesItsLine() =
        let fault = Csv.read "/stock/r.csv" ',' "a,b\n1,2\n3\n" |> expectFault Invalid

        Assert.AreEqual<string>("/stock/r.csv line 3 has 1 fields where the header has 2.", fault.Message)

    /// A record is not a line: the one after a quoted line break is counted from where
    /// its own line starts.
    [<TestMethod>]
    member _.LinesAreCountedAcrossQuotedLineBreaks() =
        let fault = Csv.read "/r.csv" ',' "a,b\n\"x\ny\",2\n3,4,5\n" |> expectFault Invalid

        StringAssert.StartsWith(fault.Message, "/r.csv line 4 ")

    [<TestMethod>]
    member _.AnUnclosedQuoteIsInvalid() =
        let fault = Csv.read "/r.csv" ',' "a\n\"never closed\n" |> expectFault Invalid

        Assert.AreEqual<string>("/r.csv line 2: a quoted field is never closed.", fault.Message)

    [<TestMethod>]
    member _.TextAfterAClosingQuoteIsInvalid() =
        Csv.read "/r.csv" ',' "a\n\"x\"y\n" |> expectFault Invalid |> ignore

    [<TestMethod>]
    member _.TwoColumnsWithOneNameAreInvalid() =
        let fault = Csv.read "/r.csv" ',' "qty,Qty\n1,2\n" |> expectFault Invalid

        Assert.AreEqual<string>("/r.csv has two columns named 'Qty'.", fault.Message)

    [<TestMethod>]
    member _.AColumnNeedsAName() =
        Csv.read "/r.csv" ',' "a,,c\n1,2,3\n" |> expectFault Invalid |> ignore

    // ---------------------------------------------------------------- writing

    [<TestMethod>]
    member _.EveryLineEndsInALineBreak() =
        let table = read "sku,qty\nC3,0\nB2,12\n"

        Assert.AreEqual<string>("sku,qty\nC3,0\nB2,12\n", write table)

    [<TestMethod>]
    member _.FieldsAreQuotedOnlyWhenTheyHaveToBe() =
        let table =
            Table.ofColumns
                [ "plain"; "comma"; "quote"; "break"; "empty" ]
                [ [ text "bolts"; text "a, b"; text "say \"hi\""; text "one\ntwo"; text "" ] ]

        Assert.AreEqual<string>(
            "plain,comma,quote,break,empty\nbolts,\"a, b\",\"say \"\"hi\"\"\",\"one\ntwo\",\"\"\n",
            write table)

    [<TestMethod>]
    member _.AGapIsAnEmptyField() =
        let table = Table.ofColumns [ "a"; "b" ] [ [ Value.Number 1.0; Value.None ] ]

        Assert.AreEqual<string>("a,b\n1,\n", write table)

    [<TestMethod>]
    member _.ATableWithNoColumnsIsTheEmptyFile() =
        Assert.AreEqual<string>("", write Table.empty)

    // ------------------------------------------------------------- round trips

    [<TestMethod>]
    member _.QuotedCommasAndLineBreaksRoundTrip() =
        let table =
            Table.ofColumns
                [ "name"; "note"; "qty" ]
                [ [ text "bolts"; text "a, b"; Value.Number 120.0 ]
                  [ text "nuts"; text "say \"hi\"\nand go"; Value.Number 12.5 ] ]

        Assert.AreEqual<Table>(table, Csv.read "/t.csv" ',' (write table) |> expectOk)

    /// The plan's case: gaps are written empty and read back as gaps.
    [<TestMethod>]
    member _.ATableWithGapsRoundTrips() =
        let table =
            Table.ofColumns
                [ "sku"; "qty"; "note" ]
                [ [ text "A1"; Value.None; text "x" ]; [ Value.None; Value.Number 3.0; Value.None ] ]

        Assert.AreEqual<Table>(table, Csv.read "/t.csv" ',' (write table) |> expectOk)

    [<TestMethod>]
    member _.AOneColumnTableWithAGapRoundTrips() =
        let table = Table.ofColumns [ "a" ] [ [ text "x" ]; [ Value.None ]; [ text "" ] ]

        Assert.AreEqual<Table>(table, Csv.read "/t.csv" ',' (write table) |> expectOk)

    // ------------------------------------------------------------ as commands

    [<TestMethod>]
    member _.ToCsvWritesAFileOfKindCsv() =
        let harness = seeded ()

        match harness.Run "ls | select name kind | to-csv listing" with
        | Value.File file -> Assert.AreEqual<string>("csv", file.Kind)
        | other -> Assert.Fail(sprintf "Expected a file, got %s." (Value.kind other))

        StringAssert.StartsWith(harness.Content "/listing", "name,kind\ndocuments,folder\n")

    [<TestMethod>]
    member _.ToCsvCoercesATableShapedTag() =
        let harness = seeded ()
        harness.Run "<t><r a=1 b=x/><r a=2/></t> | to-csv t.csv" |> ignore

        Assert.AreEqual<string>("a,b\n1,x\n2,\n", harness.Content "/t.csv")

    [<TestMethod>]
    member _.ToCsvNeedsATable() =
        let harness = seeded ()

        Assert.AreEqual<string>("'to-csv' needs a table, not text.", harness.Error "echo hello | to-csv t.csv")

    [<TestMethod>]
    member _.FromCsvReadsAFileAsATable() =
        let harness = Harness(Seed.standardFiles @ [ { Name = "stock.csv"; Folder = "/"; Content = Some "sku,qty\nA1,120\nB2,12\n" } ])

        Assert.AreEqual<string>("sku  qty\nB2   12\nA1   120", harness.Text "from-csv stock.csv | sort qty")

    [<TestMethod>]
    member _.FromCsvTakesADelimiter() =
        let harness = Harness(Seed.standardFiles @ [ { Name = "stock.csv"; Folder = "/"; Content = Some "sku;qty\nA1;120\n" } ])

        Assert.AreEqual<string list>([ "120" ], harness.Column "from-csv stock.csv -delimiter \";\"" "qty")

    [<TestMethod>]
    member _.ABadDelimiterIsInvalid() =
        let harness = seeded ()
        harness.Run "ls | to-csv l.csv" |> ignore

        Assert.AreEqual<string>(
            "'delimiter' must be one character, or 'tab', not 'ab'.",
            harness.Error "from-csv l.csv -delimiter ab")

    [<TestMethod>]
    member _.ToCsvAndFromCsvRoundTripThroughTheStore() =
        let harness = seeded ()
        harness.Run "<t><r sku=A1 qty=120/><r sku=B2 qty=12/></t> | to-csv t.csv" |> ignore

        Assert.AreEqual<string>("sku  qty\nB2   12\nA1   120", harness.Text "from-csv t.csv | sort qty")

    [<TestMethod>]
    member _.UndoTakesAWrittenCsvAway() =
        let harness = seeded ()
        harness.Run "ls | to-csv l.csv" |> ignore
        harness.Run "undo" |> ignore

        Assert.IsFalse(harness.Exists "/l.csv")
