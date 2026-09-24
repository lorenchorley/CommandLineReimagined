namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core

/// <summary>The two strings every value can answer with, and how numbers are written.</summary>
/// <remarks>
/// The distinction between `display` and `argument` is the whole of why a value is not
/// just a string: `ls` shows `notes.txt` and `ls | read` has to hand over
/// `/documents/notes.txt`. A test for each case keeps the two from collapsing into one.
/// </remarks>
[<TestClass>]
type ValueTests() =

    let ref name kind folder : FileRef =
        { Id = "id"; Name = name; Kind = kind; Folder = folder }

    [<TestMethod>]
    member _.EmptyAndNoneBothDisplayAsNothing() =
        Assert.AreEqual<string>("", Value.display Value.Empty)
        Assert.AreEqual<string>("", Value.display Value.None)

    /// They read the same and mean different things, which is what the kind carries.
    [<TestMethod>]
    member _.EmptyAndNoneAreDifferentKinds() =
        Assert.AreEqual<string>("empty", Value.kind Value.Empty)
        Assert.AreEqual<string>("none", Value.kind Value.None)

    [<TestMethod>]
    member _.TextDisplaysItself() =
        Assert.AreEqual<string>("hello", Value.display (Value.Text "hello"))

    [<TestMethod>]
    member _.BooleansDisplayAsWords() =
        Assert.AreEqual<string>("true", Value.display (Value.Boolean true))
        Assert.AreEqual<string>("false", Value.display (Value.Boolean false))

    /// `0.###`: no trailing zeros, no thousands separator, and the same under every
    /// culture. A terminal that printed `1,5` on a French machine would be wrong.
    [<DataTestMethod>]
    [<DataRow(42.0, "42")>]
    [<DataRow(42.5, "42.5")>]
    [<DataRow(42.125, "42.125")>]
    [<DataRow(42.1256, "42.126")>]
    [<DataRow(-5.0, "-5")>]
    [<DataRow(0.0, "0")>]
    [<DataRow(1000000.0, "1000000")>]
    member _.NumbersFormatInvariantly(input: float, expected: string) =
        Assert.AreEqual<string>(expected, Value.display (Value.Number input))

    /// The two-string rule for a file: its name to read, its path to pass on.
    [<TestMethod>]
    member _.AFileShowsItsNameAndArguesItsPath() =
        let value = Value.File(ref "notes.txt" "text" "/documents")

        Assert.AreEqual<string>("notes.txt", Value.display value)
        Assert.AreEqual<string>("/documents/notes.txt", Value.argument value)

    /// The root has no trailing separator, so joining to it must not double the slash.
    [<TestMethod>]
    member _.AFileAtTheRootArguesASingleSlash() =
        let value = Value.File(ref "readme.txt" "text" "/")

        Assert.AreEqual<string>("/readme.txt", Value.argument value)

    [<TestMethod>]
    member _.AFolderIsItsOwnKind() =
        Assert.AreEqual<string>("folder", Value.kind (Value.File(ref "documents" "folder" "/")))
        Assert.AreEqual<string>("file", Value.kind (Value.File(ref "notes.txt" "text" "/")))

    [<TestMethod>]
    member _.AListJoinsItsItemsWithSpaces() =
        let value = Value.List [ Value.Text "a"; Value.Number 2.0; Value.Text "c" ]

        Assert.AreEqual<string>("a 2 c", Value.display value)

    [<TestMethod>]
    member _.AnEmptyListDisplaysAsNothing() =
        Assert.AreEqual<string>("", Value.display (Value.List []))

    /// A tag reads back the way it was written, which is what makes
    /// `<file path=documents/notes.txt/>` an identity at the terminal.
    [<TestMethod>]
    member _.AnObjectTagReadsBackAsItself() =
        let tag =
            Tag.create "file" [ "path", Value.Text "documents/notes.txt" ] []

        Assert.AreEqual<string>("<file path=documents/notes.txt/>", Value.display (Value.Object tag))

    [<TestMethod>]
    member _.AComponentTagUsesBraces() =
        let tag =
            Tag.create "renderer" [ "colour", Value.Text "red" ] []

        Assert.AreEqual<string>("{renderer colour=red/}", Value.display (Value.Component tag))

    [<TestMethod>]
    member _.ATagWithNoAttributesHasNoSpace() =
        let tag = Tag.create "thing" [] []

        Assert.AreEqual<string>("<thing/>", Value.display (Value.Object tag))

    /// The later value wins and the name is written once, where it first appeared.
    [<TestMethod>]
    member _.AnAttributeWrittenTwiceReadsBackOnce() =
        let tag = Tag.create "t" [ "a", Value.Number 1.0; "b", Value.Number 3.0; "a", Value.Number 2.0 ] []

        Assert.AreEqual<string>("<t a=2 b=3/>", Value.display (Value.Object tag))

    [<TestMethod>]
    member _.ATagWithChildrenUsesTheLongForm() =
        let inner = Tag.create "inner" [] []

        let outer = Tag.create "outer" [] [ Value.Object inner ]

        Assert.AreEqual<string>("<outer><inner/></outer>", Value.display (Value.Object outer))

    /// The grammar cannot tell `42` from `notes.txt`; this is where the difference is
    /// decided, and where `echo -5` becomes a number rather than a word.
    [<DataTestMethod>]
    [<DataRow("42", true)>]
    [<DataRow("-5", true)>]
    [<DataRow("4.5", true)>]
    [<DataRow("notes.txt", false)>]
    [<DataRow("4.5.6", false)>]
    [<DataRow("", false)>]
    [<DataRow("hello", false)>]
    member _.WordsThatReadAsNumbersBecomeNumbers(word: string, expected: bool) =
        match Value.ofWord word with
        | Value.Number _ -> Assert.IsTrue(expected, sprintf "'%s' should not have been a number." word)
        | Value.Text _ -> Assert.IsFalse(expected, sprintf "'%s' should have been a number." word)
        | other -> Assert.Fail(sprintf "'%s' became %A." word other)

    /// A number that came from a word displays the way it was written, so `echo 42`
    /// does not answer `42.0`.
    [<TestMethod>]
    member _.ANumberFromAWordReadsBackUnchanged() =
        Assert.AreEqual<string>("42", Value.display (Value.ofWord "42"))
        Assert.AreEqual<string>("-5", Value.display (Value.ofWord "-5"))
