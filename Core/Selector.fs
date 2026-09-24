/// CSS selectors over a tag's tree (decision 0049).
///
/// `pick` reads a tag as a document whose root is that tag and answers every element a
/// selector matches. Compiled after `Table.fs`, since the answer is a table, and before
/// the commands, which call it.
namespace CommandLineReimagined.Core

open System

[<RequireQualifiedAccess>]
module Selector =

    /// The subset of CSS decision 0049 names, as the reference to build against.
    let supported =
        [ "type"; "*"; "[attr]"; "[attr=value]"; "[attr^=value]"; "[attr$=value]"; "[attr*=value]"
          "descendant ( )"; "child (>)"; "groups (,)" ]

    // -------------------------------------------------------------------- Types

    /// One test in brackets. Names and values are compared exactly, as XML compares them.
    [<RequireQualifiedAccess>]
    type Test =
        /// `[a]`
        | Has of name: string
        /// `[a=v]`
        | Is of name: string * value: string
        /// `[a^=v]`
        | StartsWith of name: string * value: string
        /// `[a$=v]`
        | EndsWith of name: string * value: string
        /// `[a*=v]`
        | Contains of name: string * value: string

    /// <summary>A type and its tests, written together: `book[year]`.</summary>
    /// <remarks>`Element` is `None` for `*`, and for tests written with no type.</remarks>
    type Compound = { Element: string option; Tests: Test list }

    /// What stands between two compounds.
    [<RequireQualifiedAccess>]
    type Combinator =
        /// A space: anywhere inside.
        | Descendant
        /// `>`: directly inside.
        | Child

    /// <summary>One selector of a group: compounds, left to right.</summary>
    /// <remarks>
    /// Each compound carries the combinator written before it; the first one's is
    /// `Descendant` and means nothing, since nothing is before it.
    /// </remarks>
    type Chain = (Combinator * Compound) list

    /// A selector as written and as read: the groups the commas separate.
    type Selector = { Text: string; Groups: Chain list }

    // ------------------------------------------------------------------ Parsing

    /// <summary>The fault for a selector outside the subset, saying where it stopped.</summary>
    /// <remarks>
    /// The position is counted from one, as a person counts the characters they typed,
    /// and the character is named, since a selector is short and the count alone makes
    /// you count.
    /// </remarks>
    let private stopped (text: string) (at: int) (why: string) : Outcome<'T> =
        let where =
            if at >= text.Length then "at its end"
            else sprintf "at character %d, '%c'" (at + 1) text[at]

        Error(Fault.syntax (sprintf "The selector '%s' stops %s: %s." text where why))

    /// What a character outside the subset is told.
    let private outside (c: char) =
        sprintf "'%c' is not part of the CSS that pick reads: names, *, [attribute] tests, spaces, > and commas" c

    let private isNameStart (c: char) = Char.IsLetter c || c = '_'

    /// An XML name's characters, without `.` and `:`, which CSS gives other meanings.
    let private isNameChar (c: char) = Char.IsLetterOrDigit c || c = '_' || c = '-'

    let private isQuote (c: char) = c = '"' || c = '\''

    /// Whether a selector can name an element or an attribute as it is written.
    let isName (text: string) =
        text.Length > 0 && isNameStart text[0] && Seq.forall isNameChar text

    /// <summary>Reads a selector, or says where it stopped.</summary>
    /// <remarks>
    /// Recursive descent over the text, with the position in a mutable: a selector is a
    /// line long, and every rule either moves on or stops with a fault.
    /// </remarks>
    let parse (text: string) : Outcome<Selector> =
        let n = text.Length
        let mutable i = 0

        let at (index: int) = if index < n then text[index] else '\000'

        let skipSpaces () =
            while i < n && Char.IsWhiteSpace text[i] do
                i <- i + 1

        let name () =
            let start = i

            while i < n && isNameChar text[i] do
                i <- i + 1

            text.Substring(start, i - start)

        let value (operator: string) : Outcome<string> =
            skipSpaces ()

            if i >= n then
                stopped text i (sprintf "a value is expected after '%s'" operator)
            elif isQuote text[i] then
                let quote = text[i]
                let opening = i
                let closing = text.IndexOf(quote, i + 1)

                if closing < 0 then
                    stopped text opening "the quote is not closed"
                else
                    i <- closing + 1
                    Ok(text.Substring(opening + 1, closing - opening - 1))
            else
                let start = i

                while i < n && not (Char.IsWhiteSpace text[i]) && text[i] <> ']' && text[i] <> '['
                      && not (isQuote text[i]) do
                    i <- i + 1

                if i = start then
                    stopped text i (sprintf "a value is expected after '%s'" operator)
                else
                    Ok(text.Substring(start, i - start))

        let attribute () : Outcome<Test> =
            // At the `[`.
            i <- i + 1
            skipSpaces ()

            if not (isNameStart (at i)) then
                stopped text i "an attribute name is expected after '['"
            else
                let attributeName = name ()
                skipSpaces ()

                let close (test: Test) =
                    skipSpaces ()

                    if at i = ']' then
                        i <- i + 1
                        Ok test
                    else
                        stopped text i "']' is expected after the value"

                let operator (written: string) (make: string -> Test) =
                    i <- i + written.Length

                    value written |> Result.bind (make >> close)

                match at i, at (i + 1) with
                | ']', _ ->
                    i <- i + 1
                    Ok(Test.Has attributeName)
                | '=', _ -> operator "=" (fun v -> Test.Is(attributeName, v))
                | '^', '=' -> operator "^=" (fun v -> Test.StartsWith(attributeName, v))
                | '$', '=' -> operator "$=" (fun v -> Test.EndsWith(attributeName, v))
                | '*', '=' -> operator "*=" (fun v -> Test.Contains(attributeName, v))
                | ('~' | '|'), '=' ->
                    stopped
                        text
                        i
                        (sprintf "'%c=' is not part of the CSS that pick reads, whose tests are =, ^=, $= and *=" text[i])
                | _ when i >= n -> stopped text i "']' is expected to close '['"
                | _ -> stopped text i "']' or one of =, ^=, $= and *= is expected after the attribute name"

        /// `after` says what came before, for the message when nothing follows it.
        let compound (after: string) : Outcome<Compound> =
            let element =
                if at i = '*' then
                    i <- i + 1
                    Some Option.None
                elif isNameStart (at i) then
                    Some(Some(name ()))
                else
                    Option.None

            let rec tests (acc: Test list) : Outcome<Test list> =
                if at i = '[' then
                    attribute () |> Result.bind (fun test -> tests (test :: acc))
                else
                    Ok(List.rev acc)

            tests []
            |> Result.bind (fun tests ->
                match element, tests with
                | Option.None, [] when i >= n ->
                    stopped text i (sprintf "an element name, '*' or '[' is expected %s" after)
                | Option.None, [] when Char.IsLetterOrDigit text[i] || text[i] = ',' || text[i] = '>' ->
                    stopped text i (sprintf "an element name, '*' or '[' is expected %s" after)
                | Option.None, [] -> stopped text i (outside text[i])
                | element, tests ->
                    Ok
                        { Element = element |> Option.flatten
                          Tests = tests })

        let chain (after: string) : Outcome<Chain> =
            let rec more (acc: (Combinator * Compound) list) : Outcome<Chain> =
                let before = i
                skipSpaces ()
                let spaced = i > before

                if i >= n || text[i] = ',' then
                    Ok(List.rev acc)
                elif text[i] = '>' then
                    i <- i + 1
                    skipSpaces ()
                    compound "after '>'" |> Result.bind (fun next -> more ((Combinator.Child, next) :: acc))
                elif spaced then
                    compound "after a space"
                    |> Result.bind (fun next -> more ((Combinator.Descendant, next) :: acc))
                else
                    stopped text i (outside text[i])

            compound after |> Result.bind (fun first -> more [ Combinator.Descendant, first ])

        let rec groups (acc: Chain list) (after: string) : Outcome<Chain list> =
            skipSpaces ()

            chain after
            |> Result.bind (fun read ->
                if at i = ',' then
                    i <- i + 1
                    groups (read :: acc) "after ','"
                else
                    Ok(List.rev (read :: acc)))

        if String.IsNullOrWhiteSpace text then
            Error(Fault.syntax "The selector is empty: write an element name, such as 'book', or '*' for every element.")
        else
            groups [] "at the start" |> Result.map (fun groups -> { Text = text; Groups = groups })

    // ----------------------------------------------------------------- Matching

    let private childTags (tag: Tag) =
        tag.Children
        |> List.choose (fun child ->
            match child with
            | Value.Object child
            | Value.Component child -> Some child
            | _ -> Option.None)

    /// Every element of a document, the root first, in document order, each with its
    /// ancestors, nearest first.
    let rec private walk (ancestors: Tag list) (tag: Tag) : (Tag * Tag list) list =
        (tag, ancestors) :: (childTags tag |> List.collect (walk (tag :: ancestors)))

    /// <summary>Whether an element passes one test.</summary>
    /// <remarks>
    /// An attribute's value compares as its display text, so `[year=1965]` finds the
    /// number the document read. As in CSS, a prefix, suffix or substring that is empty
    /// matches nothing.
    /// </remarks>
    let private passes (tag: Tag) (test: Test) =
        let text name = tag.Attributes |> Map.tryFind name |> Option.map Value.display

        let nonEmpty name (value: string) (check: string -> bool) =
            value <> "" && (text name |> Option.exists check)

        match test with
        | Test.Has name -> Map.containsKey name tag.Attributes
        | Test.Is(name, value) -> text name = Some value
        | Test.StartsWith(name, value) -> nonEmpty name value (fun t -> t.StartsWith(value, StringComparison.Ordinal))
        | Test.EndsWith(name, value) -> nonEmpty name value (fun t -> t.EndsWith(value, StringComparison.Ordinal))
        | Test.Contains(name, value) -> nonEmpty name value (fun t -> t.Contains(value, StringComparison.Ordinal))

    let private fits (compound: Compound) (tag: Tag) =
        (compound.Element |> Option.forall (fun name -> String.Equals(name, tag.TypeName, StringComparison.Ordinal)))
        && List.forall (passes tag) compound.Tests

    /// A chain, read from its last compound back towards the root, as browsers read one.
    let rec private matchesFrom (reversed: (Combinator * Compound) list) (tag: Tag) (ancestors: Tag list) =
        match reversed with
        | [] -> true
        | (combinator, compound) :: rest ->
            fits compound tag
            && (List.isEmpty rest
                || match combinator with
                   | Combinator.Child ->
                       match ancestors with
                       | parent :: above -> matchesFrom rest parent above
                       | [] -> false
                   | Combinator.Descendant ->
                       let rec up (remaining: Tag list) =
                           match remaining with
                           | ancestor :: above -> matchesFrom rest ancestor above || up above
                           | [] -> false

                       up ancestors)

    /// <summary>The elements of a document the selector matches.</summary>
    /// <remarks>
    /// The root included, in document order, and each once however many groups match
    /// it: one walk, and an element is kept when any group matches it.
    /// </remarks>
    let matches (selector: Selector) (root: Tag) : Tag list =
        let chains = selector.Groups |> List.map List.rev

        walk [] root
        |> List.filter (fun (tag, ancestors) -> chains |> List.exists (fun chain -> matchesFrom chain tag ancestors))
        |> List.map fst

    // ---------------------------------------------------------------- Documents

    /// <summary>A document `pick` reads: the tag it searches, and the element that is.</summary>
    /// <remarks>
    /// The two are one tag when `pick` is handed a tag. A row of a table `pick` answered
    /// is read back as a new tag, `Root`, and stands for the element it was made from,
    /// `Element`, which is what says whether it sits inside another document (decision
    /// 0052). A row that cannot say stands for itself.
    /// </remarks>
    type Document = { Root: Tag; Element: Tag }

    /// <summary>The element each row of `pick`'s answer was made from, by its `@children` cell.</summary>
    /// <remarks>
    /// A table has no room for anything it does not show, and the answer must not grow
    /// a column for this (decision 0052), so the link is kept beside it. It is keyed by
    /// the cell, a value made afresh for each row, since the list inside it can be the
    /// one empty list every childless element shares; the cell travels with the row
    /// through `where`, `sort`, `select` and a variable, and a table rebuilt from text
    /// loses it, when its rows stand for themselves. Weak, so an answer nothing holds
    /// any more takes its links with it.
    /// </remarks>
    let private madeFrom = System.Runtime.CompilerServices.ConditionalWeakTable<Value, Tag>()

    /// Every element inside the documents' roots, not the roots themselves, by reference.
    let private inside (documents: Document list) =
        let found = System.Collections.Generic.HashSet<Tag>(HashIdentity.Reference)

        let rec add (tag: Tag) =
            for child in childTags tag do
                if found.Add child then add child

        for document in documents do
            add document.Root

        found

    /// <summary>The documents worth searching (decision 0052).</summary>
    /// <remarks>
    /// One that sits inside another is not searched again, and neither is an element
    /// handed twice, so each element is answered once, in the order of the document it
    /// was found in. Two tags that are equal but separate are two elements, since what
    /// is compared is which element it is, not what it says.
    /// </remarks>
    let private searched (documents: Document list) : Document list =
        let inner = inside documents
        let seen = System.Collections.Generic.HashSet<Tag>(HashIdentity.Reference)

        documents |> List.filter (fun document -> not (inner.Contains document.Element) && seen.Add document.Element)

    /// Each element found, as the tag to show and the element it is.
    let private found (select: Tag -> Tag list) (documents: Document list) : (Tag * Tag) list =
        searched documents
        |> List.collect (fun document ->
            select document.Root
            |> List.map (fun tag -> tag, (if obj.ReferenceEquals(tag, document.Root) then document.Element else tag)))

    /// Every element of the documents, each root first, in document order, each once:
    /// what `*` picks.
    let elements (documents: Document list) : Tag list =
        found (walk [] >> List.map fst) documents |> List.map fst

    /// The distinct element names of documents, in document order: what a selector's
    /// name could be.
    let names (documents: Document list) : string list =
        elements documents |> List.map (fun tag -> tag.TypeName) |> List.distinct

    // -------------------------------------------------------------------- Table

    /// The column that holds an element's name.
    let tagColumn = "@tag"

    /// The column that holds an element's children, as a list.
    let childrenColumn = "@children"

    /// <summary>The table decision 0049 answers.</summary>
    /// <remarks>
    /// `@tag`, then the attributes of the elements in the order they first appear, a
    /// gap where an element lacks one, then `@children`. No element at all is the
    /// table with `@tag` and `@children` only. Each row's `@children` cell remembers the
    /// element the row was made from (`madeFrom`).
    /// </remarks>
    let private table (elements: (Tag * Tag) list) : Table =
        let attributes =
            elements
            |> List.collect (fun (tag, _) -> Value.orderedAttributes tag |> List.map fst)
            |> List.distinct

        let rows =
            elements
            |> List.map (fun (tag, element) ->
                let children = Value.List tag.Children
                madeFrom.AddOrUpdate(children, element)

                Value.Text tag.TypeName
                :: (attributes
                    |> List.map (fun name -> tag.Attributes |> Map.tryFind name |> Option.defaultValue Value.None))
                @ [ children ])

        Table.ofColumns ((tagColumn :: attributes) @ [ childrenColumn ]) rows

    /// Whether a table's rows are elements: whether it has an `@tag` column.
    let isElements (table: Table) = Table.indexOf table tagColumn |> Option.isSome

    /// <summary>A row of an element table, read back as its element.</summary>
    /// <remarks>
    /// `@tag` is its name, `@children` its children, the other columns its attributes,
    /// gaps left out, as `Table.rowTag` leaves them out; and it stands for the element
    /// the row was made from, when the row can say. `index` counts from one, for the
    /// fault when a row has no name.
    /// </remarks>
    let private ofRow (command: string) (table: Table) (index: int) (row: Value list) : Outcome<Document> =
        let name = Table.cell table tagColumn row

        if Value.isAbsent name then
            Error(Fault.create Invalid (sprintf "'%s' cannot read row %d as an element: its @tag is empty." command index))
        else
            let cell = Table.cell table childrenColumn row

            let children =
                match cell with
                | Value.List children -> children
                | value when Value.isAbsent value -> []
                | value -> [ value ]

            let own (column: string) =
                not (String.Equals(column, tagColumn, StringComparison.OrdinalIgnoreCase))
                && not (String.Equals(column, childrenColumn, StringComparison.OrdinalIgnoreCase))

            let attributes =
                List.zip (Table.names table) row
                |> List.filter (fun (column, value) -> own column && not (Value.isAbsent value))

            let root = Tag.create (Value.display name) attributes children

            let element =
                match madeFrom.TryGetValue cell with
                | true, element -> element
                | _ -> root

            Ok { Root = root; Element = element }

    /// <summary>What `pick` was handed, as the documents it reads.</summary>
    /// <remarks>
    /// A tag is one document; a list of tags is that many; a table whose rows have
    /// `@tag` is one per row, read back as its element, so `pick` reads what `pick`
    /// answered. Anything else is a fault naming what it was.
    /// </remarks>
    let documents (command: string) (value: Value) : Outcome<Document list> =
        let needs (what: string) =
            Error(
                Fault.create
                    Binding
                    (sprintf "'%s' needs a tag, a list of tags or a table with a @tag column, not %s." command what)
            )

        // A single row of an element table, such as `first` answers, is read back as
        // its element the way a table's rows are, so its `@tag` and `@children` are
        // the element's own parts and not two more attributes.
        let itself (index: int) (tag: Tag) =
            if tag.Attributes |> Map.exists (fun name _ -> String.Equals(name, tagColumn, StringComparison.OrdinalIgnoreCase)) then
                let cells = Value.orderedAttributes tag
                let table = Table.ofColumns (cells |> List.map fst) [ cells |> List.map snd ]
                ofRow command table index (List.head table.Rows)
            else
                Ok { Root = tag; Element = tag }

        match value with
        | Value.Object tag
        | Value.Component tag -> itself 1 tag |> Result.map List.singleton
        | Value.List items ->
            items
            |> List.mapi (fun index item -> index + 1, item)
            |> Outcome.traverse (fun (index, item) ->
                match item with
                | Value.Object tag
                | Value.Component tag -> itself index tag
                | other -> needs (sprintf "a list whose item %d is %s" index (Value.kind other)))
        | Value.Table table when isElements table ->
            table.Rows
            |> List.mapi (fun index row -> index + 1, row)
            |> Outcome.traverse (fun (index, row) -> ofRow command table index row)
        | Value.Table _ -> needs "a table without a @tag column"
        | other -> needs (Value.kind other)

    /// Every element of the documents the selector matches, as the table of 0049, each
    /// once however the documents overlap (decision 0052).
    let pick (selector: Selector) (documents: Document list) : Table =
        found (matches selector) documents |> table
