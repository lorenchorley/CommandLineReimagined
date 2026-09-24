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
        harness.Run "in documents" |> ignore

        Assert.AreEqual<string>("/documents", harness.Location)

    [<TestMethod>]
    member _.QuotedArgumentBinds() =
        let harness = seeded ()
        harness.Run "in \"documents\"" |> ignore

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
        let message = harness.Error "in"

        StringAssert.Contains(message, "needs an argument")
        StringAssert.Contains(message, "TargetPath")

    /// The count names what was supplied rather than what remains, which used to read
    /// as "takes 1 argument(s), but 1 more were given".
    [<TestMethod>]
    member _.TooManyArgumentsAreReported() =
        let harness = seeded ()

        StringAssert.Contains(harness.Error "in documents extra", "takes 1 argument, but 2 were given")

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

        Assert.AreEqual<FaultKind>(Binding, (harness.Fail "in").Kind)
        Assert.AreEqual<FaultKind>(NotFound, (harness.Fail "in nowhere").Kind)
        Assert.AreEqual<FaultKind>(UnknownCommand, (harness.Fail "nosuchcommand").Kind)

    /// The stage number is stamped by the evaluator, which is the only thing that
    /// knows where in the line the failure was.
    [<TestMethod>]
    member _.AFaultKnowsWhichStageFailed() =
        let harness = seeded ()

        Assert.AreEqual<int option>(Some 1, (harness.Fail "in nowhere").Stage)
        Assert.AreEqual<int option>(Some 2, (harness.Fail "echo nowhere | in").Stage)

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
        harness.Run "echo documents | in" |> ignore

        Assert.AreEqual<string>("/documents", harness.Location)

    [<TestMethod>]
    member _.LaterPipeStageFailureIsReported() =
        let harness = seeded ()

        StringAssert.Contains(harness.Error "echo nowhere | in", "Directory does not exist")

    /// <summary>A line is one transaction (decision 0015).</summary>
    /// <remarks>
    /// The case the record was written for. The `mkdir` succeeded and the `in` did not,
    /// and the folder must not survive: a failed line leaves no trace, so the user does
    /// not have to know how many stages ran before the failure in order to undo it.
    /// </remarks>
    [<TestMethod>]
    member _.AFailedLineLeavesNothingBehind() =
        let harness = seeded ()

        StringAssert.Contains(harness.Error "mkdir a | in nowhere", "Directory does not exist")
        Assert.IsFalse(harness.Exists "a", "The mkdir should have been rolled back with the line.")
        Assert.AreEqual<string list>([ "seed" ], harness.History())

    /// A later stage sees what an earlier one did, because the stages share a working
    /// projection even though nothing has been committed yet.
    [<TestMethod>]
    member _.ALaterStageSeesAnEarlierStagesEffect() =
        let harness = seeded ()
        harness.Run "mkdir scratch | in" |> ignore

        Assert.AreEqual<string>("/scratch", harness.Location)

    [<TestMethod>]
    member _.AWholeLineIsUndoneAtOnce() =
        let harness = seeded ()
        harness.Run "mkdir one | in" |> ignore

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

        StringAssert.Contains(harness.Error "in nowhere", "Directory does not exist")

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

        Assert.AreEqual<string>("documents examples guide projects zebra apple.txt readme.txt", harness.Names "ls")

    /// <summary>A listing is records and nothing else (Phase 3).</summary>
    /// <remarks>
    /// Until Phase 3 a listing below the root began with a parent entry that navigated
    /// rather than naming a record. A table has nowhere to put one — every row is a
    /// record — so the page offers `out` in the location line instead, and the `out`
    /// command is what it runs.
    /// </remarks>
    [<TestMethod>]
    member _.AListingHasNoParentRow() =
        let harness = seeded ()

        Assert.AreEqual<string>("documents examples guide projects readme.txt", harness.Names "ls")

        harness.Run "in documents" |> ignore
        Assert.AreEqual<string>("notes.txt", harness.Names "ls")

    /// `ls | in` with no quoting rule: the parent entry argues where it goes.
    [<TestMethod>]
    member _.TheParentEntryNavigates() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore

        harness.Run "out" |> ignore

        Assert.AreEqual<string>("/", harness.Location)

    // ------------------------------------------------------------ value stages

    /// Decision 0032: a variable standing alone is a line, and its value is the answer.
    [<TestMethod>]
    member _.AVariableStandingAloneAnswersItsValue() =
        let harness = seeded ()
        harness.Run "set v 5" |> ignore

        Assert.AreEqual<Value>(Value.Number 5.0, harness.Run "$v")
        Assert.AreEqual<string>("5", harness.Text "$v")

    /// A value stage is the head of a pipeline like any other, so what it holds flows on.
    [<TestMethod>]
    member _.AVariableStageFeedsThePipe() =
        let harness = seeded ()
        harness.Run "ls | set files" |> ignore

        Assert.AreEqual<Value>(Value.Number 5.0, harness.Run "$files | count")
        Assert.AreEqual<string>("documents examples guide projects", harness.Names "$files | where $row.kind eq folder")

    /// Members read off a value stage the way they read off an argument.
    [<TestMethod>]
    member _.AVariableStageReadsItsMembers() =
        let harness = seeded ()
        harness.Run "try read missing.txt | set problem" |> ignore

        Assert.AreEqual<string>("NotFound", harness.Text "$problem.kind")

    /// A value stage changes nothing, so a live view may re-read it.
    [<TestMethod>]
    member _.AVariableStageOnlyReads() =
        let harness = seeded ()
        harness.Run "set v 5" |> ignore

        let response = harness.Refresh "$v"

        Assert.IsTrue(response.Fault.IsNone)
        Assert.AreEqual<Value option>(Some(Value.Number 5.0), response.Result)

    [<TestMethod>]
    member _.AnUnknownVariableStageIsReported() =
        let harness = seeded ()

        Assert.AreEqual<string>("Unknown variable: $nope", harness.Error "$nope")
        Assert.AreEqual<string>("Unknown variable: $nope", harness.Error "echo $nope")

    /// Decision 0032: `$row` outside a predicate says where it does exist.
    [<TestMethod>]
    member _.RowStandingAloneSaysWhereItExists() =
        let harness = seeded ()

        let fault = harness.Fail "$row"

        Assert.AreEqual<string>(
            "$row is the row a predicate is testing. It exists only inside where, find, in and save-view: ls | where $row.kind eq folder.",
            fault.Message)
        Assert.AreEqual(NotFound, fault.Kind)

    /// The same for `$row` read as an argument, which used to be an unknown variable.
    [<TestMethod>]
    member _.RowAsAnArgumentSaysWhereItExists() =
        let harness = seeded ()

        StringAssert.StartsWith(harness.Error "echo $row", "$row is the row a predicate is testing.")
        StringAssert.StartsWith(harness.Error "$row.kind", "$row is the row a predicate is testing.")

    /// Inside a predicate `$row` is the row, as it always was.
    [<TestMethod>]
    member _.RowInsideAPredicateIsTheRow() =
        let harness = seeded ()

        Assert.AreEqual<string>("readme.txt", harness.Names "ls | where $row.kind eq text")

/// <summary>Guidance when a line goes wrong (Phase 9, stream B).</summary>
/// <remarks>
/// Decision 0038: a line that called a command wrongly fails with the same fault as
/// before, and carries that command's help as its guide: the description `help`
/// writes, then the table it answers. Decision 0037: an old name leads to the new one.
/// </remarks>
[<TestClass>]
type GuidanceTests() =

    /// What `help <command>` says, the way the guide carries it.
    static let helpOf (harness: Harness) (command: string) =
        let response = harness.Respond(sprintf "help %s" command)
        Value.List((response.Output |> List.map Value.Text) @ [ Option.get response.Result ])

    static let guideOf (harness: Harness) (line: string) =
        let response = harness.Respond line
        Assert.IsTrue(response.Fault.IsSome, sprintf "'%s' did not fail." line)
        response.Guide

    static let assertGuide (harness: Harness) (line: string) (command: string) =
        match guideOf harness line with
        | Some guide -> Assert.AreEqual<string>(Value.display (helpOf harness command), Value.display guide, line)
        | None -> Assert.Fail(sprintf "'%s' carried no guide." line)

    static let assertNoGuide (harness: Harness) (line: string) =
        Assert.AreEqual<Value option>(None, guideOf harness line, line)

    // ------------------------------------------------------------ 0038

    /// `read` with no argument: the fault, and under it `read`'s help.
    [<TestMethod>]
    member _.AMissingArgumentCarriesTheCommandsHelp() =
        let harness = seeded ()
        let response = harness.Respond "read"

        Assert.AreEqual<string>("'read' needs an argument for 'path'.", response.Fault.Value.Message)

        match response.Guide with
        | Some(Value.List [ Value.Text description; Value.Table table ]) ->
            Assert.AreEqual<string>("Show what a file says", description)
            Assert.AreEqual<string list>([ "name"; "required"; "piped"; "takes"; "description" ], Table.names table)
            Assert.AreEqual<int>(1, List.length table.Rows)
        | other -> Assert.Fail(sprintf "%A" other)

    /// Every way of calling a command wrongly, at any stage of the line.
    [<TestMethod>]
    member _.EveryWrongCallCarriesTheCommandsHelp() =
        let harness = seeded ()

        for line, command in
            [ "read", "read"
              // Too many: the command called wrongly is `help`, so the guide is its own.
              "help where extra", "help"
              "vars x", "vars"
              "ls -nosuch", "ls"
              "vars name=x", "vars"
              "ls | sort", "sort"
              "ls | sort name sideways", "sort"
              "ls | where", "where"
              "ls | select", "select"
              "set x", "set"
              "echo 1 | sort name", "sort" ] do
            assertGuide harness line command

    /// The fault is the one it always was, and so is what `try` and `else` see of it.
    [<TestMethod>]
    member _.TheFaultIsUnchanged() =
        let harness = seeded ()

        Assert.AreEqual<string>("'help' takes 1 argument, but 2 were given.", harness.Error "help where extra")
        Assert.AreEqual<FaultKind>(Binding, (harness.Fail "help where extra").Kind)
        Assert.AreEqual<Value option>(None, (harness.Respond "try read").Guide)
        Assert.AreEqual<Value option>(None, (harness.Respond "read else echo fine").Guide)

    /// With `else` the fault is the last branch's, and so is the guide.
    [<TestMethod>]
    member _.AnElseLineCarriesTheLastBranchsGuide() =
        let harness = seeded ()

        assertGuide harness "read missing.txt else ls | sort" "sort"
        assertNoGuide harness "read else read missing.txt"

    /// A fault raised while a command runs, and an unknown name, carry no help.
    [<TestMethod>]
    member _.AFailureThatIsNotAWrongCallCarriesNoGuide() =
        let harness = seeded ()

        for line in [ "read missing.txt"; "cd documents"; "lss"; "ls | sort nosuch"; "in nowhere"; "echo $nothing" ] do
            assertNoGuide harness line

    /// A line that works carries no guide.
    [<TestMethod>]
    member _.ALineThatWorksCarriesNoGuide() =
        let harness = seeded ()

        for line in [ "ls"; "help read"; "read readme.txt" ] do
            Assert.AreEqual<Value option>(None, (harness.Respond line).Guide, line)

    /// <summary>A wrong call from inside a stage is not that stage's.</summary>
    /// <remarks>
    /// The fault is stamped on the stage the parenthesis or the `run` is in, and it
    /// is not that command that was called wrongly, so neither carries a guide.
    /// </remarks>
    [<TestMethod>]
    member _.AWrongCallInsideAStageIsNotTheStages() =
        let harness = seeded ()
        harness.Run "write wrong.clr \"ls | sort\"" |> ignore

        StringAssert.Contains(harness.Error "run wrong.clr", "'sort' needs an argument for 'column'.")
        assertNoGuide harness "run wrong.clr"
        assertNoGuide harness "echo (ls | sort)"
        // `run` called wrongly is its own.
        assertGuide harness "run" "run"

    /// Nothing the guide is worked out from reaches the log or the history.
    [<TestMethod>]
    member _.WorkingOutTheGuideLeavesNoTrace() =
        let harness = seeded ()
        let before = harness.History()

        harness.Respond "read" |> ignore

        Assert.AreEqual<int>(List.length before, List.length (harness.History()))
        Assert.AreEqual<string list>([], (harness.Respond "read").Output)

    /// A live refresh is nobody's line, and says nothing more than its fault.
    [<TestMethod>]
    member _.ARefreshCarriesNoGuide() =
        let harness = seeded ()

        Assert.AreEqual<Value option>(None, (harness.Refresh "ls | sort").Guide)

    // ------------------------------------------------------------ 0037

    /// `cd documents` says which command to use now, and so do the other old names.
    [<TestMethod>]
    member _.AnOldNameSaysWhichCommandToUse() =
        let harness = seeded ()

        Assert.AreEqual<string>("Unknown command : cd. Did you mean in?", harness.Error "cd documents")
        Assert.AreEqual<string>("Unknown command : up. Did you mean out?", harness.Error "up")
        Assert.AreEqual<string>("Unknown command : cat. Did you mean read?", harness.Error "cat readme.txt")
        Assert.AreEqual<string>("Unknown command : cd. Did you mean in?", harness.Error "help cd")
        Assert.AreEqual<string>("Unknown command : delete. Did you mean rm?", harness.Error "delete readme.txt")

    /// Slips are corrected as they were, and a word two commands are described by names neither.
    [<TestMethod>]
    member _.ASlipIsStillCorrected() =
        let harness = seeded ()

        Assert.AreEqual<string>("Unknown command : lss. Did you mean ls?", harness.Error "lss")
        Assert.AreEqual<string>("Unknown command : rn. Did you mean in, rm or run?", harness.Error "rn")
        Assert.AreEqual<string>("Unknown command : by", harness.Error "by")
