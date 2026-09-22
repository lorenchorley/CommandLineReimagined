namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>Binding, pipes, command forms, tags, variables and errors.</summary>
/// <remarks>
/// The port of `Execution.Tests/ExecutionTests.cs`. Every case is the same case; what
/// differs is what it asserts against. The originals created a temporary directory and
/// checked the disk afterwards, which meant the tests could only say that something
/// happened somewhere. These read the projection, so they say exactly what the line
/// changed, and they run without a filesystem at all.
/// </remarks>
[<TestClass>]
type ExecutionTests() =

    // ------------------------------------------------------------ argument binding

    [<TestMethod>]
    member _.UnquotedArgumentBinds() =
        let harness = seeded ()
        harness.Run "cd documents" |> ignore

        Assert.AreEqual<string>("/documents", harness.Location)

    [<TestMethod>]
    member _.QuotedArgumentBinds() =
        let harness = seeded ()
        harness.Run "cd \"documents\"" |> ignore

        Assert.AreEqual<string>("/documents", harness.Location)

    [<TestMethod>]
    member _.NumericArgumentBindsAsNumber() =
        let harness = seeded ()

        match harness.Run "echo 42" with
        | Value.Number n -> Assert.AreEqual<float>(42.0, n)
        | other -> Assert.Fail(sprintf "Expected a number, got %A" other)

    [<TestMethod>]
    member _.MissingRequiredArgumentIsReported() =
        let harness = seeded ()
        let message = harness.Error "cd"

        StringAssert.Contains(message, "needs an argument")
        StringAssert.Contains(message, "TargetPath")

    /// The count names what was supplied rather than what remains, which used to read
    /// as "takes 1 argument(s), but 1 more were given".
    [<TestMethod>]
    member _.TooManyArgumentsAreReported() =
        let harness = seeded ()

        StringAssert.Contains(harness.Error "cd documents extra", "takes 1 argument, but 2 were given")

    [<TestMethod>]
    member _.UnknownNamedArgumentIsReported() =
        let harness = seeded ()

        StringAssert.Contains(harness.Error "ls -nonsense value", "no argument named")

    [<TestMethod>]
    member _.NamedArgumentBindsOutOfPosition() =
        let harness = seeded ()

        Assert.AreEqual<string>("notes.txt", harness.Names "ls -path documents")

    [<TestMethod>]
    member _.AFaultCarriesItsKind() =
        let harness = seeded ()

        Assert.AreEqual<FaultKind>(Binding, (harness.Fail "cd").Kind)
        Assert.AreEqual<FaultKind>(NotFound, (harness.Fail "cd nowhere").Kind)
        Assert.AreEqual<FaultKind>(UnknownCommand, (harness.Fail "nosuchcommand").Kind)

    /// The stage number is stamped by the evaluator, which is the only thing that
    /// knows where in the line the failure was.
    [<TestMethod>]
    member _.AFaultKnowsWhichStageFailed() =
        let harness = seeded ()

        Assert.AreEqual<int option>(Some 1, (harness.Fail "cd nowhere").Stage)
        Assert.AreEqual<int option>(Some 2, (harness.Fail "echo nowhere | cd").Stage)

    // ------------------------------------------------------------------------ pipes

    [<TestMethod>]
    member _.PipeRunsEveryStage() =
        let harness = seeded ()

        // echo receives mkdir's result rather than re-running mkdir.
        match harness.Run "mkdir alpha | echo" with
        | Value.File file -> Assert.AreEqual<string>("alpha", file.Name)
        | other -> Assert.Fail(sprintf "Expected a file, got %A" other)

        Assert.IsTrue(harness.Exists "alpha")

    [<TestMethod>]
    member _.PipeThreadsValueBetweenCommands() =
        let harness = seeded ()
        harness.Run "echo documents | cd" |> ignore

        Assert.AreEqual<string>("/documents", harness.Location)

    [<TestMethod>]
    member _.LaterPipeStageFailureIsReported() =
        let harness = seeded ()

        StringAssert.Contains(harness.Error "echo nowhere | cd", "Directory does not exist")

    /// <summary>A line is one transaction (decision 0015).</summary>
    /// <remarks>
    /// The case the record was written for. The `mkdir` succeeded and the `cd` did not,
    /// and the folder must not survive: a failed line leaves no trace, so the user does
    /// not have to know how many stages ran before the failure in order to undo it.
    /// </remarks>
    [<TestMethod>]
    member _.AFailedLineLeavesNothingBehind() =
        let harness = seeded ()

        StringAssert.Contains(harness.Error "mkdir a | cd nowhere", "Directory does not exist")
        Assert.IsFalse(harness.Exists "a", "The mkdir should have been rolled back with the line.")
        Assert.AreEqual<string list>([ "seed" ], harness.History())

    /// A later stage sees what an earlier one did, because the stages share a working
    /// projection even though nothing has been committed yet.
    [<TestMethod>]
    member _.ALaterStageSeesAnEarlierStagesEffect() =
        let harness = seeded ()
        harness.Run "mkdir scratch | cd" |> ignore

        Assert.AreEqual<string>("/scratch", harness.Location)

    [<TestMethod>]
    member _.AWholeLineIsUndoneAtOnce() =
        let harness = seeded ()
        harness.Run "mkdir one | cd" |> ignore

        harness.Run "undo" |> ignore

        Assert.IsFalse(harness.Exists "/one")
        Assert.AreEqual<string>("/", harness.Location)

    // --------------------------------------------------------------- command forms

    [<TestMethod>]
    member _.FunctionFormExecutes() =
        let harness = seeded ()

        Assert.AreEqual<string>("hello", harness.Text "echo(hello)")

    [<TestMethod>]
    member _.FunctionFormWithNamedArgumentExecutes() =
        let harness = seeded ()

        Assert.AreEqual<string>("hello", harness.Text "echo(text: hello)")

    [<TestMethod>]
    member _.ObjectInstanceFormEvaluates() =
        let harness = seeded ()

        match harness.Run "<measurement unit=metres/>" with
        | Value.Object tag ->
            Assert.AreEqual<string>("measurement", tag.TypeName)
            Assert.AreEqual<string>("metres", Value.display tag.Attributes["unit"])
        | other -> Assert.Fail(sprintf "Expected an object, got %A" other)

    [<TestMethod>]
    member _.NestedObjectInstanceEvaluates() =
        let harness = seeded ()

        match harness.Run "<outer><inner depth=2/></outer>" with
        | Value.Object tag ->
            Assert.AreEqual<int>(1, tag.Children.Length)

            match tag.Children[0] with
            | Value.Object inner ->
                Assert.AreEqual<string>("inner", inner.TypeName)
                Assert.AreEqual<Value>(Value.Number 2.0, inner.Attributes["depth"])
            | other -> Assert.Fail(sprintf "Expected a nested object, got %A" other)
        | other -> Assert.Fail(sprintf "Expected an object, got %A" other)

    [<TestMethod>]
    member _.EmptyCommandDoesNothing() =
        let harness = seeded ()

        Assert.AreEqual<Value>(Value.Empty, harness.Run "")

    // ------------------------------------------------------------------- variables

    /// <summary>A tag that names a variable binds it, as an event.</summary>
    /// <remarks>
    /// The old implementation mutated the scope where it stood. Now it is a
    /// `VariableChanged` in the line's transaction, so the binding survives a reload
    /// and comes back out on `undo` with everything else the line did.
    /// </remarks>
    [<TestMethod>]
    member _.ObjectInstanceBindsVariable() =
        let harness = seeded ()
        harness.Run "<size|measurement unit=metres/>" |> ignore

        match harness.Variable "size" with
        | Some(Value.Object tag) -> Assert.AreEqual<string>("measurement", tag.TypeName)
        | other -> Assert.Fail(sprintf "Expected $size to be an object, got %A" other)

    [<TestMethod>]
    member _.UndoOfATagBindingUnbindsTheVariable() =
        let harness = seeded ()
        harness.Run "<size|measurement unit=metres/>" |> ignore

        harness.Run "undo" |> ignore

        Assert.IsTrue((harness.Variable "size").IsNone)

    [<TestMethod>]
    member _.VariableReferenceResolvesAsArgument() =
        let harness = seeded ()
        harness.Run "<target|folder name=documents/>" |> ignore

        match harness.Run "echo $target" with
        | Value.Object _ -> ()
        | other -> Assert.Fail(sprintf "Expected an object, got %A" other)

    [<TestMethod>]
    member _.UnknownVariableIsReported() =
        let harness = seeded ()

        StringAssert.Contains(harness.Error "echo $missing", "Unknown variable")

    // -------------------------------------------------------- tags as arguments

    [<TestMethod>]
    member _.TagIsAcceptedAsAnArgument() =
        let harness = seeded ()

        match harness.Run "echo <measurement unit=metres/>" with
        | Value.Object tag ->
            Assert.AreEqual<string>("measurement", tag.TypeName)
            Assert.AreEqual<string>("metres", Value.display tag.Attributes["unit"])
        | other -> Assert.Fail(sprintf "Expected an object, got %A" other)

    [<TestMethod>]
    member _.ComponentTagIsAcceptedAsAnArgument() =
        let harness = seeded ()

        match harness.Run "echo {renderer/}" with
        | Value.Component _ -> ()
        | other -> Assert.Fail(sprintf "Expected a component, got %A" other)

    [<TestMethod>]
    member _.TagAsAnArgumentStillBindsItsVariable() =
        let harness = seeded ()
        harness.Run "echo <size|measurement unit=metres/>" |> ignore

        match harness.Variable "size" with
        | Some(Value.Object _) -> ()
        | other -> Assert.Fail(sprintf "Expected $size to be an object, got %A" other)

    [<TestMethod>]
    member _.TagIsAcceptedAsAFunctionArgument() =
        let harness = seeded ()

        match harness.Run "echo(<thing/>)" with
        | Value.Object _ -> ()
        | other -> Assert.Fail(sprintf "Expected an object, got %A" other)

    [<TestMethod>]
    member _.VariableTagIsAcceptedAsAnArgument() =
        let harness = seeded ()
        harness.Run "<size|measurement unit=metres/>" |> ignore

        match harness.Run "echo <$size>" with
        | Value.Object tag -> Assert.AreEqual<string>("measurement", tag.TypeName)
        | other -> Assert.Fail(sprintf "Expected an object, got %A" other)

    // ------------------------------------------------------------- component tags

    [<TestMethod>]
    member _.ComponentTagEvaluates() =
        let harness = seeded ()

        match harness.Run "{renderer colour=red/}" with
        | Value.Component tag ->
            Assert.AreEqual<string>("renderer", tag.TypeName)
            Assert.AreEqual<string>("red", Value.display tag.Attributes["colour"])
        | other -> Assert.Fail(sprintf "Expected a component, got %A" other)

    [<TestMethod>]
    member _.ComponentTagBindsAVariable() =
        let harness = seeded ()
        harness.Run "{handle|renderer/}" |> ignore

        match harness.Variable "handle" with
        | Some(Value.Component _) -> ()
        | other -> Assert.Fail(sprintf "Expected $handle to be a component, got %A" other)

    [<TestMethod>]
    member _.ComponentReadsBackTheWayItWasWritten() =
        let harness = seeded ()

        Assert.AreEqual<string>("{renderer colour=red/}", harness.Text "{renderer colour=red/}")

    [<TestMethod>]
    member _.EntityMayContainAComponent() =
        let harness = seeded ()

        match harness.Run "<player>{renderer/}</player>" with
        | Value.Object tag -> Assert.AreEqual<string>("player", tag.TypeName)
        | other -> Assert.Fail(sprintf "Expected an object, got %A" other)

    [<TestMethod>]
    member _.VariableTagReadsTheVariableBack() =
        let harness = seeded ()
        harness.Run "<size|measurement unit=metres/>" |> ignore

        match harness.Run "<$size>" with
        | Value.Object tag -> Assert.AreEqual<string>("measurement", tag.TypeName)
        | other -> Assert.Fail(sprintf "Expected an object, got %A" other)

    [<TestMethod>]
    member _.VariableTagForAnUnknownNameIsReported() =
        let harness = seeded ()

        StringAssert.Contains(harness.Error "<$missing>", "Unknown variable")

    // ---------------------------------------------------------------------- errors

    [<TestMethod>]
    member _.UnknownCommandIsReported() =
        let harness = seeded ()

        StringAssert.Contains(harness.Error "nosuchcommand", "Unknown command")

    /// The command that reports unknown names is not itself a name you can type: typed,
    /// it reported an unknown command with no name.
    [<TestMethod>]
    member _.TypingTheUnknownCommandsNameNamesIt() =
        let harness = seeded ()

        let fault = harness.Fail "UnknownCommand"

        Assert.AreEqual<FaultKind>(UnknownCommand, fault.Kind)
        Assert.AreEqual<string>("Unknown command : UnknownCommand", fault.Message)

    [<TestMethod>]
    member _.FailingCommandReportsWhyRatherThanSilentlyDoingNothing() =
        let harness = seeded ()

        StringAssert.Contains(harness.Error "cd nowhere", "Directory does not exist")

    /// A parse failure is a fault like any other, so the session boundary never raises.
    [<TestMethod>]
    member _.ASyntaxErrorIsAFaultRatherThanAnException() =
        let harness = seeded ()

        Assert.AreEqual<FaultKind>(Syntax, (harness.Fail "<thing").Kind)

    // --------------------------------------------------------------------- results

    [<TestMethod>]
    member _.ListDirectoryReturnsEntries() =
        let harness = seeded ()

        let names = harness.Column "ls" "name"

        assertContains "documents" names
        assertContains "readme.txt" names

    /// <summary>Folders first, then files, each by name (decision 0016).</summary>
    /// <remarks>
    /// Normative now. The old specification said the order "follows the filesystem",
    /// which meant it was whatever the host returned and could not be tested at all.
    /// </remarks>
    [<TestMethod>]
    member _.AListingIsFoldersThenFilesEachByName() =
        let harness = seeded ()
        harness.Run "mkdir zebra" |> ignore
        harness.Run "write apple.txt a" |> ignore

        Assert.AreEqual<string>("documents examples projects zebra apple.txt readme.txt", harness.Names "ls")

    /// <summary>A listing is records and nothing else (Phase 3).</summary>
    /// <remarks>
    /// Until Phase 3 a listing below the root began with a `up` entry that navigated
    /// rather than naming a record. A table has nowhere to put one — every row is a
    /// record — so the page offers `up` in the location line instead, and the `up`
    /// command is what it runs.
    /// </remarks>
    [<TestMethod>]
    member _.AListingHasNoParentRow() =
        let harness = seeded ()

        Assert.AreEqual<string>("documents examples projects readme.txt", harness.Names "ls")

        harness.Run "cd documents" |> ignore
        Assert.AreEqual<string>("notes.txt", harness.Names "ls")

    /// `ls | cd` with no quoting rule: the parent entry argues where it goes.
    [<TestMethod>]
    member _.TheParentEntryNavigates() =
        let harness = seeded ()
        harness.Run "cd documents" |> ignore

        harness.Run "up" |> ignore

        Assert.AreEqual<string>("/", harness.Location)
