namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness

/// <summary>Paths: making them absolute, and finding what they name.</summary>
/// <remarks>
/// Resolution is a pure function over a projection, so every one of these runs without
/// a filesystem. That is the point of the attribute store: a path is a question about
/// two attributes, `folder` and `name`, and not a question for an operating system.
/// </remarks>
[<TestClass>]
type FilesTests() =

    /// `/documents/notes.txt`, `/projects` and `/readme.txt`.
    let projection =
        Projection.applyAll
            Projection.empty
            [ FileCreated(folder "d" "documents" "/")
              FileCreated(folder "p" "projects" "/")
              FileCreated(file "r" "readme.txt" "/")
              FileCreated(file "n" "notes.txt" "/documents") ]

    let at folder = { Folder = folder; View = None }

    // ------------------------------------------------------------------ Normalisation

    [<DataTestMethod>]
    [<DataRow("/", "documents", "/documents")>]
    [<DataRow("/", "/documents", "/documents")>]
    [<DataRow("/documents", "notes.txt", "/documents/notes.txt")>]
    [<DataRow("/documents", "/readme.txt", "/readme.txt")>]
    [<DataRow("/documents", ".", "/documents")>]
    [<DataRow("/documents", "..", "/")>]
    [<DataRow("/documents", "../projects", "/projects")>]
    [<DataRow("/documents", "./notes.txt", "/documents/notes.txt")>]
    [<DataRow("/a/b/c", "../../d", "/a/d")>]
    [<DataRow("/", "/", "/")>]
    [<DataRow("/documents", "", "/documents")>]
    member _.PathsResolveAgainstWhereYouAre(here: string, written: string, expected: string) =
        Assert.AreEqual<string>(expected, Files.normalise here written)

    /// `..` at the root stays at the root. `in ..` at the top is a no-op, not an error,
    /// which is what every shell does.
    [<DataTestMethod>]
    [<DataRow("/", "..", "/")>]
    [<DataRow("/", "../..", "/")>]
    [<DataRow("/documents", "../../..", "/")>]
    member _.GoingUpFromTheRootStaysThere(here: string, written: string, expected: string) =
        Assert.AreEqual<string>(expected, Files.normalise here written)

    /// Repeated and trailing separators are noise, not structure.
    [<DataTestMethod>]
    [<DataRow("/", "documents/", "/documents")>]
    [<DataRow("/", "//documents//notes.txt", "/documents/notes.txt")>]
    member _.RedundantSeparatorsCollapse(here: string, written: string, expected: string) =
        Assert.AreEqual<string>(expected, Files.normalise here written)

    [<DataTestMethod>]
    [<DataRow("/documents/notes.txt", "/documents", "notes.txt")>]
    [<DataRow("/readme.txt", "/", "readme.txt")>]
    // The root has no name, so it splits to the root and nothing. Resolution never
    // reaches this: it answers for the root before splitting, because the root is not
    // a record (decision 0016).
    [<DataRow("/", "/", "")>]
    member _.SplittingSeparatesTheFolderFromTheName(path: string, folder: string, name: string) =
        let actualFolder, actualName = Files.split path

        Assert.AreEqual<string>(folder, actualFolder)
        Assert.AreEqual<string>(name, actualName)

    // -------------------------------------------------------------------- Resolution

    [<TestMethod>]
    member _.AnAbsolutePathFindsItsRecord() =
        let record = expectOk (Files.resolve projection (at "/") "/documents/notes.txt")

        Assert.AreEqual<string>("notes.txt", Record.name record)

    [<TestMethod>]
    member _.ARelativePathIsRelativeToWhereYouAre() =
        let record = expectOk (Files.resolve projection (at "/documents") "notes.txt")

        Assert.AreEqual<string>("n", record.Id)

    [<TestMethod>]
    member _.DotDotWalksUpBeforeLookingUp() =
        let record = expectOk (Files.resolve projection (at "/documents") "../readme.txt")

        Assert.AreEqual<string>("readme.txt", Record.name record)

    /// The message names the path as it was resolved, not as it was typed: being
    /// somewhere else than you thought is the usual cause of a missing file.
    [<TestMethod>]
    member _.AMissingFileNamesTheResolvedPath() =
        let fault = expectFault NotFound (Files.resolve projection (at "/documents") "nope.txt")

        Assert.AreEqual<string>("File does not exist : /documents/nope.txt", fault.Message)
        Assert.AreEqual<string option>(Some "/documents/nope.txt", fault.Path)

    /// The root is not a record, so asking for it as a file says what it is instead of
    /// claiming it does not exist.
    [<TestMethod>]
    member _.TheRootIsNotAFile() =
        let fault = expectFault Invalid (Files.resolve projection (at "/") "/")

        Assert.AreEqual<string>("That is a directory, not a file : /", fault.Message)

    [<TestMethod>]
    member _.AFolderResolvesToItsPath() =
        Assert.AreEqual<string>("/documents", expectOk (Files.resolveFolder projection (at "/") "documents"))
        Assert.AreEqual<string>("/", expectOk (Files.resolveFolder projection (at "/documents") ".."))

    /// The root always exists as a folder even though nothing created it.
    [<TestMethod>]
    member _.TheRootIsAlwaysAFolder() =
        Assert.AreEqual<string>("/", expectOk (Files.resolveFolder Projection.empty (at "/") "/"))

    /// A file is not a folder, and `in` has to say so rather than moving into it.
    [<TestMethod>]
    member _.AFileDoesNotResolveAsAFolder() =
        let fault = expectFault NotFound (Files.resolveFolder projection (at "/") "readme.txt")

        Assert.AreEqual<string>("Directory does not exist : readme.txt", fault.Message)

    /// `in` reports the target as written, because what you usually need to see there
    /// is your own spelling.
    [<TestMethod>]
    member _.AMissingFolderIsNamedAsItWasWritten() =
        let fault = expectFault NotFound (Files.resolveFolder projection (at "/") "nowhere")

        Assert.AreEqual<string>("Directory does not exist : nowhere", fault.Message)

    // ---------------------------------------------------------------------- Listing

    [<TestMethod>]
    member _.AFolderHoldsOnlyItsDirectChildren() =
        let atRoot = Files.inFolder projection "/" |> List.map Record.name |> List.sort

        Assert.AreEqual<string list>([ "documents"; "projects"; "readme.txt" ], atRoot)

    [<TestMethod>]
    member _.AFolderDeeperDownHoldsItsOwn() =
        let inDocuments = Files.inFolder projection "/documents" |> List.map Record.name

        Assert.AreEqual<string list>([ "notes.txt" ], inDocuments)

    // ------------------------------------------------------------- Kind inference

    [<DataTestMethod>]
    [<DataRow("notes.txt", "text")>]
    [<DataRow("data.xml", "xml")>]
    [<DataRow("table.csv", "csv")>]
    [<DataRow("config.json", "json")>]
    [<DataRow("inventory.clr", "script")>]
    [<DataRow("readme.md", "markdown")>]
    [<DataRow("README.MD", "markdown")>]
    [<DataRow("noextension", "text")>]
    [<DataRow(".hidden", "text")>]
    [<DataRow("trailing.", "text")>]
    member _.KindIsGuessedFromTheExtension(name: string, expected: string) =
        Assert.AreEqual<string>(expected, Files.inferKind name)
