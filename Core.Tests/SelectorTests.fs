namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>CSS selectors and `pick` (decision 0049).</summary>
/// <remarks>
/// Phase 11's stream B, finding T2. The selector is pinned against the module, where a
/// selector can hold what the command line would need quoting for; `pick` is pinned
/// through a session, on the library the Acceptance table of the phase uses.
/// </remarks>
[<TestClass>]
type SelectorTests() =

    static let library =
        "<library city=paris><book title=dune year=1965><author name=herbert/></book>"
        + "<book title=emma year=1815><author name=austen/></book><shelf/></library>"

    /// A session with `$d` holding the library.
    static let withLibrary () =
        let harness = seeded ()
        harness.Run("set d " + library) |> ignore
        harness

    static let parse (text: string) = Selector.parse text |> expectOk

    static let stops (text: string) = (Selector.parse text |> expectFault Syntax).Message

    static let compound element tests : Selector.Compound = { Element = element; Tests = tests }

    static let tag name attributes children = Tag.create name attributes children

    /// The library as a value, for the tests that match against the module.
    static let document =
        let author name = Value.Object(tag "author" [ "name", text name ] [])

        tag
            "library"
            [ "city", text "paris" ]
            [ Value.Object(tag "book" [ "title", text "dune"; "year", Value.Number 1965.0 ] [ author "herbert" ])
              Value.Object(tag "book" [ "title", text "emma"; "year", Value.Number 1815.0 ] [ author "austen" ])
              Value.Object(tag "shelf" [] []) ]

    /// What a selector matches in the library, each as its name and its first attribute.
    static let matched (selector: string) =
        Selector.matches (parse selector) document
        |> List.map (fun tag ->
            match Value.orderedAttributes tag with
            | (_, value) :: _ -> tag.TypeName + ":" + Value.display value
            | [] -> tag.TypeName)

    /// The subset the decision names is the one the module says it supports.
    [<TestMethod>]
    member _.TheSubsetIsNamed() =
        Assert.AreEqual<int>(10, List.length Selector.supported)

    // ------------------------------------------------------------------ parsing

    [<TestMethod>]
    member _.ATypeIsOneCompound() =
        Assert.AreEqual<Selector.Chain list>(
            [ [ Selector.Combinator.Descendant, compound (Some "book") [] ] ],
            (parse "book").Groups
        )

    [<TestMethod>]
    member _.AStarIsAnyElement() =
        Assert.AreEqual<Selector.Chain list>([ [ Selector.Combinator.Descendant, compound None [] ] ], (parse "*").Groups)

    [<TestMethod>]
    member _.EveryAttributeTestIsRead() =
        let tests =
            [ Selector.Test.Has "a"
              Selector.Test.Is("b", "1")
              Selector.Test.StartsWith("c", "x")
              Selector.Test.EndsWith("d", "y")
              Selector.Test.Contains("e", "z") ]

        Assert.AreEqual<Selector.Chain list>(
            [ [ Selector.Combinator.Descendant, compound (Some "book") tests ] ],
            (parse "book[a][b=1][c^=x][d$=y][e*=z]").Groups
        )

    /// Tests with no type are any element that passes them.
    [<TestMethod>]
    member _.TestsAloneAreAnyElement() =
        Assert.AreEqual<Selector.Chain list>(
            [ [ Selector.Combinator.Descendant, compound None [ Selector.Test.Has "year" ] ] ],
            (parse "[year]").Groups
        )

    [<TestMethod>]
    member _.CombinatorsAndGroupsAreRead() =
        let d = Selector.Combinator.Descendant
        let c = Selector.Combinator.Child

        Assert.AreEqual<Selector.Chain list>(
            [ [ d, compound (Some "a") []; d, compound (Some "b") []; c, compound (Some "c") [] ]
              [ d, compound (Some "d") [] ] ],
            (parse "a b > c, d").Groups
        )

    /// Space around `>` and `,` and at either end changes nothing.
    [<TestMethod>]
    member _.SpacingIsFree() =
        for written in [ "a>b"; "a > b"; "  a   >b  "; "a >  b" ] do
            Assert.AreEqual<Selector.Chain list>((parse "a > b").Groups, (parse written).Groups, written)

        Assert.AreEqual<Selector.Chain list>((parse "a, b").Groups, (parse " a ,b ").Groups)
        Assert.AreEqual<Selector.Chain list>((parse "a b").Groups, (parse "a    b").Groups)

    [<TestMethod>]
    member _.AValueMayBeBareOrQuoted() =
        let value selector =
            match (parse selector).Groups with
            | [ [ _, { Tests = [ Selector.Test.Is(_, value) ] } ] ] -> value
            | other -> failwithf "read as %A" other

        Assert.AreEqual<string>("dune", value "[title=dune]")
        Assert.AreEqual<string>("dune", value "[title=\"dune\"]")
        Assert.AreEqual<string>("dune", value "[title='dune']")
        Assert.AreEqual<string>("a b] c", value "[title=\"a b] c\"]")
        Assert.AreEqual<string>("", value "[title=\"\"]")
        Assert.AreEqual<string>("1965", value "[ year = 1965 ]")

    [<TestMethod>]
    member _.TheTextIsKept() =
        Assert.AreEqual<string>("book > author", (parse "book > author").Text)

    // ------------------------------------------------------------------- faults

    /// Where it stopped, counted from one, and what was expected there.
    [<TestMethod>]
    member _.AFaultSaysWhereTheSelectorStopped() =
        Assert.AreEqual<string>(
            "The selector 'book >' stops at its end: an element name, '*' or '[' is expected after '>'.",
            stops "book >"
        )

        Assert.AreEqual<string>(
            "The selector 'book:first' stops at character 5, ':': ':' is not part of the CSS that pick reads: names, *, [attribute] tests, spaces, > and commas.",
            stops "book:first"
        )

    [<TestMethod>]
    member _.EveryWayOutOfTheSubsetIsAFault() =
        let cases =
            [ "book,", "stops at its end: an element name, '*' or '[' is expected after ','"
              ", book", "stops at character 1, ',': an element name, '*' or '[' is expected at the start"
              "book > > a", "stops at character 8, '>': an element name, '*' or '[' is expected after '>'"
              "book.new", "stops at character 5, '.'"
              "#main", "stops at character 1, '#'"
              "a + b", "stops at character 3, '+'"
              "a ~ b", "stops at character 3, '~'"
              "book[", "stops at its end: an attribute name is expected after '['"
              "book[1]", "stops at character 6, '1': an attribute name is expected after '['"
              "book[title", "stops at its end: ']' is expected to close '['"
              "book[title!=x]", "stops at character 11, '!': ']' or one of =, ^=, $= and *= is expected"
              "book[title~=x]", "stops at character 11, '~': '~=' is not part of the CSS"
              "book[title|=x]", "stops at character 11, '|': '|=' is not part of the CSS"
              "book[title=]", "stops at character 12, ']': a value is expected after '='"
              "book[title^=", "stops at its end: a value is expected after '^='"
              "book[title=\"dune]", "stops at character 12, '\"': the quote is not closed"
              "book[title=dune x]", "stops at character 17, 'x': ']' is expected after the value"
              "book[title=dune", "stops at its end: ']' is expected after the value"
              "*book", "stops at character 2, 'b'"
              "book[a]b", "stops at character 8, 'b'" ]

        for selector, expected in cases do
            let message = stops selector
            Assert.IsTrue(message.Contains expected, sprintf "%s: %s" selector message)

    [<TestMethod>]
    member _.AnEmptySelectorIsAFault() =
        for written in [ ""; "   " ] do
            StringAssert.StartsWith(stops written, "The selector is empty")

    // ----------------------------------------------------------------- matching

    [<TestMethod>]
    member _.ATypeMatchesItsElements() =
        Assert.AreEqual<string list>([ "book:dune"; "book:emma" ], matched "book")
        Assert.AreEqual<string list>([ "shelf" ], matched "shelf")
        Assert.AreEqual<string list>([], matched "magazine")

    /// Names compare exactly, as XML compares them.
    [<TestMethod>]
    member _.NamesAndValuesCompareExactly() =
        Assert.AreEqual<string list>([], matched "Book")
        Assert.AreEqual<string list>([], matched "[title=Dune]")
        Assert.AreEqual<string list>([], matched "[Title]")

    [<TestMethod>]
    member _.TheRootIsIncluded() =
        Assert.AreEqual<string list>([ "library:paris" ], matched "library")
        Assert.AreEqual<string list>([ "library:paris" ], matched "[city]")

    [<TestMethod>]
    member _.AStarIsEveryElementInDocumentOrder() =
        Assert.AreEqual<string list>(
            [ "library:paris"; "book:dune"; "author:herbert"; "book:emma"; "author:austen"; "shelf" ],
            matched "*"
        )

    [<TestMethod>]
    member _.EachAttributeTestMatches() =
        Assert.AreEqual<string list>([ "book:dune"; "book:emma" ], matched "[year]")
        Assert.AreEqual<string list>([ "book:emma" ], matched "[title=emma]")
        Assert.AreEqual<string list>([ "book:dune" ], matched "[title^=du]")
        Assert.AreEqual<string list>([ "book:dune" ], matched "[title$=ne]")
        Assert.AreEqual<string list>([ "book:dune" ], matched "[title*=un]")
        Assert.AreEqual<string list>([ "author:austen" ], matched "[name*=st]")

    /// An attribute's value compares as its display text, so a number is found by its digits.
    [<TestMethod>]
    member _.ANumberComparesAsItsText() =
        Assert.AreEqual<string list>([ "book:dune" ], matched "[year=1965]")
        Assert.AreEqual<string list>([ "book:dune" ], matched "[year^=19]")
        Assert.AreEqual<string list>([ "book:emma" ], matched "[year$=15]")

    /// As in CSS, an empty prefix, suffix or substring matches nothing.
    [<TestMethod>]
    member _.AnEmptyPartialValueMatchesNothing() =
        Assert.AreEqual<string list>([], matched "[title^='']")
        Assert.AreEqual<string list>([], matched "[title$='']")
        Assert.AreEqual<string list>([], matched "[title*='']")

    [<TestMethod>]
    member _.AMissingAttributeFailsItsTest() =
        Assert.AreEqual<string list>([], matched "shelf[title]")
        Assert.AreEqual<string list>([], matched "author[year^=1]")

    [<TestMethod>]
    member _.ACompoundNeedsEveryPart() =
        Assert.AreEqual<string list>([ "book:dune" ], matched "book[year][title^=d]")
        Assert.AreEqual<string list>([], matched "author[year]")
        Assert.AreEqual<string list>([ "book:dune"; "book:emma" ], matched "*[year]")

    [<TestMethod>]
    member _.ADescendantIsAnywhereInside() =
        Assert.AreEqual<string list>([ "author:herbert"; "author:austen" ], matched "library author")
        Assert.AreEqual<string list>([ "author:herbert" ], matched "book[title=dune] author")
        Assert.AreEqual<string list>([ "author:herbert"; "author:austen" ], matched "library book author")
        Assert.AreEqual<string list>([], matched "shelf author")

    [<TestMethod>]
    member _.AChildIsDirectlyInside() =
        Assert.AreEqual<string list>([], matched "library > author")
        Assert.AreEqual<string list>([ "author:herbert"; "author:austen" ], matched "book > author")
        Assert.AreEqual<string list>([ "author:herbert"; "author:austen" ], matched "library > book > author")
        Assert.AreEqual<string list>([ "author:herbert"; "author:austen" ], matched "library > * author")
        Assert.AreEqual<string list>([ "shelf" ], matched "library > shelf")

    /// A descendant combinator tries every ancestor, not only the nearest that fits.
    [<TestMethod>]
    member _.ADescendantLooksPastTheNearestAncestor() =
        let nested =
            tag "a" [] [ Value.Object(tag "b" [] [ Value.Object(tag "a" [ "x", text "1" ] [ Value.Object(tag "c" [] []) ]) ]) ]

        let found selector =
            Selector.matches (parse selector) nested |> List.map (fun t -> t.TypeName)

        Assert.AreEqual<string list>([ "c" ], found "a > b c")
        Assert.AreEqual<string list>([ "c" ], found "a > b > a > c")
        Assert.AreEqual<string list>([ "a"; "a" ], found "a")
        Assert.AreEqual<string list>([ "a" ], found "b a")

    /// Groups answer in document order, not the order the groups are written, and an
    /// element that more than one group matches is there once.
    [<TestMethod>]
    member _.GroupsAnswerInDocumentOrderEachOnce() =
        Assert.AreEqual<string list>([ "author:herbert"; "author:austen"; "shelf" ], matched "shelf, author")

        Assert.AreEqual<string list>(
            [ "book:dune"; "author:herbert"; "book:emma"; "author:austen" ],
            matched "author, book, book > author, [year]"
        )

        Assert.AreEqual<string list>(matched "*", matched "*, *, book")

    /// Only tags are elements: a child that is text is walked past.
    [<TestMethod>]
    member _.OnlyTagsAreElements() =
        let mixed = tag "note" [] [ text "hello"; Value.Object(tag "b" [] []); Value.Number 3.0 ]
        Assert.AreEqual<int>(2, Selector.matches (parse "*") mixed |> List.length)

    // -------------------------------------------------------------------- table

    [<TestMethod>]
    member _.TheTableIsTagTheAttributesInFirstSeenOrderThenChildren() =
        let harness = withLibrary ()
        let table = harness.Table "$d | pick book"

        Assert.AreEqual<string list>([ "@tag"; "title"; "year"; "@children" ], Table.names table)
        Assert.AreEqual<string list>([ "book"; "book" ], harness.Column "$d | pick book" "@tag")
        Assert.AreEqual<string list>([ "dune"; "emma" ], harness.Column "$d | pick book" "title")

        Assert.AreEqual<string list>(
            [ "@tag"; "city"; "title"; "year"; "name"; "@children" ],
            Table.names (harness.Table "$d | pick \"*\"")
        )

    /// Decision 0009's types, read off the cells: `year` is a number column.
    [<TestMethod>]
    member _.TheColumnsAreTyped() =
        let table = withLibrary().Table "$d | pick book"

        Assert.AreEqual<ColumnType>(TextCol, table.Columns[0].Type)
        Assert.AreEqual<ColumnType>(NumberCol, table.Columns[2].Type)

    [<TestMethod>]
    member _.ChildrenAreAList() =
        let table = withLibrary().Table "$d | pick book"
        let children = table.Rows |> List.map (fun row -> Table.cell table "@children" row)

        Assert.AreEqual<Value list>(
            [ Value.List [ Value.Object(tag "author" [ "name", text "herbert" ] []) ]
              Value.List [ Value.Object(tag "author" [ "name", text "austen" ] []) ] ],
            children
        )

        let shelf = withLibrary().Table "$d | pick shelf"
        Assert.AreEqual<Value>(Value.List [], Table.cell shelf "@children" shelf.Rows.Head)

    /// A gap where an element lacks an attribute another has.
    [<TestMethod>]
    member _.AMissingAttributeIsAGap() =
        let harness = withLibrary ()
        let table = harness.Table "$d | pick \"shelf, book\""

        Assert.AreEqual<string list>([ "book"; "book"; "shelf" ], harness.Column "$d | pick \"shelf, book\"" "@tag")
        Assert.AreEqual<Value>(Value.None, Table.cell table "title" (List.item 2 table.Rows))

    [<TestMethod>]
    member _.NoMatchIsTheEmptyTableWithTagAndChildren() =
        let table = withLibrary().Table "$d | pick magazine"

        Assert.AreEqual<string list>([ "@tag"; "@children" ], Table.names table)
        Assert.AreEqual<int>(0, List.length table.Rows)

    // ------------------------------------------------------------------ pick

    [<TestMethod>]
    member _.PickIsReadOnly() =
        let spec = (seeded ()).Session.Commands |> List.find (fun spec -> spec.Name = "pick")
        Assert.IsTrue spec.ReadOnly

    [<TestMethod>]
    member _.PickReadsAFileReadWithFromXml() =
        let harness = seeded ()

        harness.Run(
            "write lib.xml \"<library><book title='dune'><author name='herbert'/><author name='anderson'/></book>"
            + "<shelf><book title='emma'><author name='austen'/></book></shelf></library>\""
        )
        |> ignore

        Assert.AreEqual<string list>(
            [ "herbert"; "anderson"; "austen" ],
            harness.Column "from-xml lib.xml | pick author" "name"
        )

        Assert.AreEqual<string list>([ "emma" ], harness.Column "from-xml lib.xml | pick \"shelf > book\"" "title")
        Assert.AreEqual<string list>([ "dune" ], harness.Column "from-xml lib.xml | pick \"library > book\"" "title")

    /// A table whose rows have `@tag` is read back as its elements, each its own
    /// document, so the root of each is the row's element.
    [<TestMethod>]
    member _.PickReadsWhatPickAnswered() =
        let harness = withLibrary ()

        Assert.AreEqual<string list>([ "herbert"; "austen" ], harness.Column "$d | pick book | pick author" "name")
        Assert.AreEqual<string list>([ "dune"; "emma" ], harness.Column "$d | pick book | pick book" "title")
        Assert.AreEqual<string list>([], harness.Column "$d | pick book | pick \"library book\"" "title")

        Assert.AreEqual<string list>(
            [ "herbert" ],
            harness.Column "$d | pick book | where $row.year gt 1900 | pick author" "name"
        )

    /// The row's other columns are its attributes, gaps left out, so a test on an
    /// attribute the row lacks fails as it does on the element.
    [<TestMethod>]
    member _.ARowReadBackKeepsItsAttributes() =
        let harness = withLibrary ()

        Assert.AreEqual<string list>([ "dune" ], harness.Column "$d | pick book | pick \"[year^=19]\"" "title")
        Assert.AreEqual<string list>([ "book"; "book" ], harness.Column "$d | pick \"shelf, book\" | pick \"[title]\"" "@tag")
        Assert.AreEqual<string list>([ "shelf" ], harness.Column "$d | pick \"shelf, book\" | pick \"shelf\"" "@tag")

    [<TestMethod>]
    member _.AListOfTagsIsThatManyDocuments() =
        let children = Value.List document.Children
        let documents = Selector.documents "pick" children |> expectOk

        Assert.AreEqual<int>(3, List.length documents)

        Assert.AreEqual<string list>(
            [ "book"; "author"; "book"; "author"; "shelf" ],
            Selector.pick (parse "*") documents |> fun table -> table.Rows |> List.map (List.head >> Value.display)
        )

        Assert.AreEqual<int>(0, Selector.documents "pick" (Value.List []) |> expectOk |> List.length)

    // ------------------------------------------------- 0052: each element once

    /// The rows of `pick "*"` overlap: a book is a row of its own and inside the
    /// library's. The book's row is not searched again.
    [<TestMethod>]
    member _.OverlappingRowsAnswerEachElementOnce() =
        let harness = withLibrary ()

        Assert.AreEqual<string list>([ "dune" ], harness.Column "$d | pick \"*\" | pick \"[year^=19]\" | select title" "title")

        Assert.AreEqual<string list>(
            [ "library"; "book"; "author"; "book"; "author"; "shelf" ],
            harness.Column "$d | pick \"*\" | pick \"*\"" "@tag"
        )

        Assert.AreEqual<string list>([ "herbert"; "austen" ], harness.Column "$d | pick \"*\" | pick author" "name")
        Assert.AreEqual<string list>([ "herbert"; "austen" ], harness.Column "$d | pick \"*\" | pick \"book > author\"" "name")

    /// Picking again and again from what `*` answered finds the same elements, once.
    [<TestMethod>]
    member _.PickingFromOverlappingRowsIsStable() =
        let harness = withLibrary ()

        Assert.AreEqual<string list>(
            harness.Column "$d | pick \"*\"" "@tag",
            harness.Column "$d | pick \"*\" | pick \"*\" | pick \"*\"" "@tag"
        )

    /// The rows keep what they stand for through the table functions and a variable.
    [<TestMethod>]
    member _.ARowStandsForItsElementThroughTheTableFunctions() =
        let harness = withLibrary ()

        for middle in
            [ "where $row.@tag ne shelf"
              "sort \"@tag\""
              "sort \"@tag\" desc"
              "select \"@tag\" \"@children\" year title"
              "take 6"
              "skip 0" ] do
            let line = sprintf "$d | pick \"*\" | %s | pick \"[year^=19]\"" middle
            Assert.AreEqual<string list>([ "dune" ], harness.Column line "title", line)

        harness.Run "set all ($d | pick \"*\")" |> ignore
        Assert.AreEqual<string list>([ "dune" ], harness.Column "$all | pick \"[year^=19]\"" "title")

    /// A document inside another is not searched again, whichever comes first, and the
    /// elements come in the order of the document they were found in.
    [<TestMethod>]
    member _.ADocumentInsideAnotherIsNotSearchedAgain() =
        let book = document.Children.Head

        let names (items: Value list) =
            Selector.documents "pick" (Value.List items)
            |> expectOk
            |> Selector.pick (parse "*")
            |> fun table -> table.Rows |> List.map (List.head >> Value.display)

        let all = [ "library"; "book"; "author"; "book"; "author"; "shelf" ]

        Assert.AreEqual<string list>(all, names [ Value.Object document; book ])
        Assert.AreEqual<string list>(all, names [ book; Value.Object document ])
        Assert.AreEqual<string list>([ "book"; "author" ], names [ book; book ])

        // Sorted, the inner rows come first: the library's row is still the one searched.
        let harness = withLibrary ()

        Assert.AreEqual<string list>(
            [ "library"; "book"; "author"; "book"; "author"; "shelf" ],
            harness.Column "$d | pick \"*\" | sort \"@tag\" desc | pick \"*\"" "@tag"
        )

    /// Equal is not the same: two elements typed alike are two, as documents in a
    /// list, as children of one document, and as rows.
    [<TestMethod>]
    member _.EqualButSeparateElementsAreTwo() =
        let harness = seeded ()
        harness.Run "set p <pair><author name=x/><author name=x/></pair>" |> ignore

        Assert.AreEqual<string list>([ "x"; "x" ], harness.Column "$p.@children | pick author" "name")
        Assert.AreEqual<string list>([ "x"; "x" ], harness.Column "$p | pick author" "name")
        Assert.AreEqual<string list>([ "x"; "x" ], harness.Column "$p | pick author | pick author" "name")
        Assert.AreEqual<string list>([ "x"; "x" ], harness.Column "$p | pick \"*\" | pick author" "name")

        let twice =
            [ Value.Object(tag "author" [ "name", text "x" ] []); Value.Object(tag "author" [ "name", text "x" ] []) ]

        Assert.AreEqual<int>(2, Selector.documents "pick" (Value.List twice) |> expectOk |> Selector.elements |> List.length)

    /// Childless elements share F#'s one empty list, and are still told apart.
    [<TestMethod>]
    member _.ChildlessElementsAreToldApart() =
        let harness = seeded ()
        harness.Run "set s <shelves><shelf/><shelf/><shelf/></shelves>" |> ignore

        Assert.AreEqual<int>(3, harness.Column "$s | pick shelf | pick shelf" "@tag" |> List.length)
        Assert.AreEqual<int>(3, harness.Column "$s | pick \"*\" | pick shelf" "@tag" |> List.length)

    /// A list of tags inside a document: `$d.@children` are the library's own children.
    [<TestMethod>]
    member _.AListOfChildrenIsReadAsThatManyDocuments() =
        let harness = withLibrary ()

        Assert.AreEqual<string list>(
            [ "book"; "author"; "book"; "author"; "shelf" ],
            harness.Column "$d.@children | pick \"*\" | select \"@tag\"" "@tag"
        )

    [<TestMethod>]
    member _.AnythingElseIsAFaultNamingWhatItWas() =
        let harness = withLibrary ()

        Assert.AreEqual<string>(
            "'pick' needs a tag, a list of tags or a table with a @tag column, not number.",
            harness.Error "echo 5 | pick book"
        )

        Assert.AreEqual<string>(
            "'pick' needs a tag, a list of tags or a table with a @tag column, not a table without a @tag column.",
            harness.Error "ls | pick book"
        )

        StringAssert.EndsWith(harness.Error "pick book", "not empty.")

        let listFault =
            Selector.documents "pick" (Value.List [ Value.Object document; text "x" ]) |> expectFault Binding

        StringAssert.EndsWith(listFault.Message, "not a list whose item 2 is text.")

    [<TestMethod>]
    member _.ASelectorFaultIsSyntaxThroughTheSession() =
        let fault = withLibrary().Fail "$d | pick \"book >\""

        Assert.AreEqual<FaultKind>(Syntax, fault.Kind)
        StringAssert.Contains(fault.Message, "stops at its end")

    /// The selector is read before the document, so a bad one is its own fault
    /// whatever was piped.
    [<TestMethod>]
    member _.TheSelectorIsReadFirst() =
        Assert.AreEqual<FaultKind>(Syntax, (withLibrary().Fail "echo 5 | pick \"a >\"").Kind)

    // ------------------------------------------------------------- acceptance

    /// The Acceptance lines of Phase 11 that do not need stream A's `@` members. `select
    /// @tag` is written `select "@tag"`: a bare word cannot start with `@`.
    [<TestMethod>]
    member _.AcceptanceLinesWithoutMembers() =
        let harness = withLibrary ()

        let bookTable = harness.Table "$d | pick book"
        Assert.AreEqual<string list>([ "@tag"; "title"; "year"; "@children" ], Table.names bookTable)
        Assert.AreEqual<string list>([ "dune"; "emma" ], harness.Column "$d | pick book" "title")

        Assert.AreEqual<string list>(
            [ "herbert"; "austen" ],
            harness.Column "$d | pick \"book > author\" | select name" "name"
        )

        Assert.AreEqual<string list>(
            [ "library"; "book"; "author"; "book"; "author"; "shelf" ],
            harness.Column "$d | pick \"*\" | select \"@tag\"" "@tag"
        )

        Assert.AreEqual<string list>([ "dune" ], harness.Column "$d | pick \"book[year^=19]\" | select title" "title")

        Assert.AreEqual<string list>(
            [ "author"; "author"; "shelf" ],
            harness.Column "$d | pick \"shelf, author\"" "@tag"
        )

        Assert.AreEqual<string list>(
            [ "dune" ],
            harness.Column "$d | pick book | where $row.year gt 1900 | select title" "title"
        )

        Assert.AreEqual<string list>([ "herbert"; "austen" ], harness.Column "$d | pick book | pick author | select name" "name")

        let fault = harness.Fail "$d | pick \"book >\""
        Assert.AreEqual<FaultKind>(Syntax, fault.Kind)
        StringAssert.Contains(fault.Message, "'book >' stops at its end")

        // `$v.a` reads as before.
        harness.Run "set v <thing a=1/>" |> ignore
        Assert.AreEqual<string>("1", harness.Text "echo $v.a")
