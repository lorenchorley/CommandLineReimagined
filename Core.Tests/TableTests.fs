namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness

/// <summary>The Table value: how one is built, and what it reads as.</summary>
/// <remarks>
/// Decision 0009 is what most of this pins down: a tag is a table when it is
/// table-shaped, columns are the union of the rows' attributes, a gap is `None`, and a
/// tag that is not table-shaped says which child broke the shape rather than failing
/// with "not a table".
/// </remarks>
[<TestClass>]
type TableTests() =

    let item attributes = Value.Object(Tag.create "item" attributes [])

    let parent children = Tag.create "items" [] children

    let noSize (_: FileRecord) = 0.0

    // --------------------------------------------------------------- From tags

    [<TestMethod>]
    member _.AWellFormedTagBecomesATable() =
        let tag =
            parent
                [ item [ "sku", Value.Text "A1"; "qty", Value.Number 120.0 ]
                  item [ "sku", Value.Text "B2"; "qty", Value.Number 12.0 ] ]

        let table = expectOk (Table.ofTag tag)

        CollectionAssert.AreEqual([| "sku"; "qty" |], Table.names table |> Array.ofList)
        Assert.AreEqual<int>(2, List.length table.Rows)

    /// The columns are the union, in the order they first appear, so a table read out of
    /// a document keeps the document's order rather than an alphabetical one.
    [<TestMethod>]
    member _.ColumnsAreTheUnionInTheOrderTheyFirstAppear() =
        let tag =
            parent
                [ item [ "sku", Value.Text "A1"; "qty", Value.Number 1.0 ]
                  item [ "sku", Value.Text "B2"; "name", Value.Text "nuts" ] ]

        let table = expectOk (Table.ofTag tag)

        CollectionAssert.AreEqual([| "sku"; "qty"; "name" |], Table.names table |> Array.ofList)

    /// Decision 0009: a missing cell is `None`, not a short row and not a failure.
    [<TestMethod>]
    member _.AMissingAttributeIsAGap() =
        let tag =
            parent [ item [ "sku", Value.Text "A1"; "qty", Value.Number 1.0 ]; item [ "sku", Value.Text "B2" ] ]

        let table = expectOk (Table.ofTag tag)
        let second = List.item 1 table.Rows

        Assert.AreEqual<int>(2, List.length second)
        Assert.AreEqual<Value>(Value.None, Table.cell table "qty" second)

    [<TestMethod>]
    member _.AChildOfADifferentTypeIsNotATable() =
        let tag =
            parent [ item [ "sku", Value.Text "A1" ]; Value.Object(Tag.create "thing" [ "sku", Value.Text "B2" ] []) ]

        let fault = expectFault Invalid (Table.ofTag tag)

        StringAssert.Contains(fault.Message, "child 2")
        StringAssert.Contains(fault.Message, "'thing'")

    [<TestMethod>]
    member _.AChildWithChildrenIsNotATable() =
        let nested = Value.Object(Tag.create "item" [] [ Value.Object(Tag.create "part" [] []) ])
        let fault = expectFault Invalid (Table.ofTag (parent [ item [ "sku", Value.Text "A1" ]; nested ]))

        StringAssert.Contains(fault.Message, "child 2")
        StringAssert.Contains(fault.Message, "children of its own")

    [<TestMethod>]
    member _.ATagWithNoChildrenIsAnEmptyTable() =
        let table = expectOk (Table.ofTag (parent []))

        Assert.AreEqual<int>(0, List.length table.Rows)
        Assert.AreEqual<int>(0, List.length table.Columns)

    // ------------------------------------------------------------ Column types

    /// The type comes from the data, which is what makes `sort qty` numeric on a table
    /// that was never declared.
    [<TestMethod>]
    member _.AColumnIsTypedWhenEveryCellAgrees() =
        let tag =
            parent [ item [ "qty", Value.Number 1.0 ]; item [ "qty", Value.Number 2.0 ] ]

        let table = expectOk (Table.ofTag tag)

        Assert.AreEqual<ColumnType>(NumberCol, (List.head table.Columns).Type)

    [<TestMethod>]
    member _.AColumnWithDisagreeingCellsIsMixed() =
        let tag =
            parent [ item [ "qty", Value.Number 1.0 ]; item [ "qty", Value.Text "many" ] ]

        let table = expectOk (Table.ofTag tag)

        Assert.AreEqual<ColumnType>(MixedCol, (List.head table.Columns).Type)

    /// A gap says nothing about the column's type, or one missing cell would turn a
    /// number column into one that sorts alphabetically.
    [<TestMethod>]
    member _.AGapDoesNotMakeAColumnMixed() =
        let tag = parent [ item [ "qty", Value.Number 1.0 ]; item [ "sku", Value.Text "B2" ] ]

        let table = expectOk (Table.ofTag tag)

        Assert.AreEqual<ColumnType>(NumberCol, (List.head table.Columns).Type)

    // ---------------------------------------------------------------- Records

    [<TestMethod>]
    member _.ARecordTableHasTheListingColumns() =
        let table = Table.ofRecords noSize [ file "id-1" "notes.txt" "/" ]

        CollectionAssert.AreEqual(
            [| "name"; "kind"; "folder"; "size"; "modified" |],
            Table.names table |> Array.ofList)

    /// `created` is on every record and is never what a listing is for.
    [<TestMethod>]
    member _.ARecordTableDoesNotShowCreated() =
        let stamped =
            { file "id-1" "notes.txt" "/" with
                Attributes =
                    (file "id-1" "notes.txt" "/").Attributes
                    |> Map.add Attributes.created (Value.Text "2026-09-21") }

        let table = Table.ofRecords noSize [ stamped ]

        CollectionAssert.DoesNotContain(Table.names table |> Array.ofList, "created")

    [<TestMethod>]
    member _.UserAttributesBecomeColumnsOrderedByName() =
        let tagged =
            { file "id-1" "notes.txt" "/" with
                Attributes =
                    (file "id-1" "notes.txt" "/").Attributes
                    |> Map.add "tag" (Value.Text "work")
                    |> Map.add "mood" (Value.Text "good") }

        let table = Table.ofRecords noSize [ tagged ]

        CollectionAssert.AreEqual(
            [| "name"; "kind"; "folder"; "size"; "modified"; "mood"; "tag" |],
            Table.names table |> Array.ofList)

    /// The name cell is the record itself, so tapping it in the browser inserts a path
    /// rather than a word that happens to look like one.
    [<TestMethod>]
    member _.TheNameColumnHoldsTheRecord() =
        let table = Table.ofRecords noSize [ file "id-1" "notes.txt" "/" ]

        Assert.AreEqual<ColumnType>(FileCol, (List.head table.Columns).Type)

    [<TestMethod>]
    member _.SizeIsWhateverTheCallerMeasured() =
        let table = Table.ofRecords (fun _ -> 41.0) [ file "id-1" "readme.txt" "/" ]

        Assert.AreEqual<Value>(Value.Number 41.0, Table.cell table "size" (List.head table.Rows))

    // ------------------------------------------------------------------- Rows

    /// A row reads in column order, not alphabetically: the whole point of a tag
    /// keeping the order it was built in.
    [<TestMethod>]
    member _.ARowReadsInColumnOrder() =
        let table = Table.ofRecords (fun _ -> 41.0) [ file "id-1" "readme.txt" "/" ]

        Assert.AreEqual<string>(
            "<row name=readme.txt kind=text folder=/ size=41/>",
            Value.display (Table.row table (List.head table.Rows)))

    /// A gap is an attribute that is not there, rather than one that is empty.
    [<TestMethod>]
    member _.ARowLeavesOutItsGaps() =
        let tag = parent [ item [ "sku", Value.Text "A1"; "qty", Value.Number 1.0 ]; item [ "sku", Value.Text "B2" ] ]
        let table = expectOk (Table.ofTag tag)

        Assert.AreEqual<string>("<row sku=B2/>", Value.display (Table.row table (List.item 1 table.Rows)))

    [<TestMethod>]
    member _.ATableAndItsTagAreInverses() =
        let tag = parent [ item [ "sku", Value.Text "A1" ]; item [ "sku", Value.Text "B2" ] ]
        let table = expectOk (Table.ofTag tag)
        let back = expectOk (Table.ofTag (Table.toTag "items" table))

        Assert.AreEqual<Table>(table, back)

    // -------------------------------------------------------------- Coercion

    [<TestMethod>]
    member _.AListOfSameTypedTagsIsATable() =
        let table =
            expectOk (Table.ofValue "where" (Value.List [ item [ "sku", Value.Text "A1" ]; item [ "sku", Value.Text "B2" ] ]))

        Assert.AreEqual<int>(2, List.length table.Rows)

    [<TestMethod>]
    member _.TextIsNotATable() =
        let fault = expectFault Binding (Table.ofValue "where" (Value.Text "hello"))

        StringAssert.Contains(fault.Message, "'where' needs a table, not text.")

    // --------------------------------------------------------------- Display

    /// Columns are padded to the widest thing in them, with two spaces between, and no
    /// trailing spaces on a line.
    [<TestMethod>]
    member _.ATableReadsAsAlignedText() =
        let tag =
            parent
                [ item [ "sku", Value.Text "A1"; "name", Value.Text "bolts" ]
                  item [ "sku", Value.Text "B2"; "name", Value.Text "nuts" ] ]

        let table = expectOk (Table.ofTag tag)

        Assert.AreEqual<string>("sku  name\nA1   bolts\nB2   nuts", Value.display (Value.Table table))

    [<TestMethod>]
    member _.ATableIsItsOwnKind() =
        Assert.AreEqual<string>("table", Value.kind (Value.Table Table.empty))
