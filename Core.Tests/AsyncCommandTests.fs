namespace CommandLineReimagined.Core.Tests

open System
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.Harness
open CommandLineReimagined.Core.Tests.SessionHarness

/// Answers every request with the same body, so `download` can be tested without a
/// network and without a live host's availability deciding whether the suite passes.
type private StubHandler(body: string) =
    inherit HttpMessageHandler()

    override _.SendAsync(_, _) =
        let response = new HttpResponseMessage(HttpStatusCode.OK)
        response.Content <- new StringContent(body)
        Task.FromResult response

/// <summary>Long-running commands: progress, cancellation and download.</summary>
/// <remarks>
/// The port of `Execution.Tests/AsyncCommandTests.cs`. Cancellation is a fault now
/// rather than an exception, which is the visible half of decision 0006: a cancelled
/// line reports `Stopped.` through the same path as every other failure and commits
/// nothing.
/// </remarks>
[<TestClass>]
type AsyncCommandTests() =

    [<TestMethod>]
    member _.ProgressRunsToCompletionAndReturnsThePercentage() =
        let harness = bare ()
        let response = harness.Respond "progress 4 1"

        Assert.AreEqual<Value option>(Some(Value.Number 100.0), response.Result)
        assertContains "100%" response.Output
        assertContains "Progress test finished" response.Output

    [<TestMethod>]
    member _.ProgressAcceptsNamedArguments() =
        let harness = bare ()

        Assert.AreEqual<Value>(Value.Number 100.0, harness.Run "progress -steps 2 -delay 1")

    [<TestMethod>]
    member _.ProgressRejectsZeroSteps() =
        let harness = bare ()

        StringAssert.Contains(harness.Error "progress 0 1", "at least 1")

    /// A bare `-steps` flag binds `true`, and the message names what it should have been.
    [<TestMethod>]
    member _.ProgressRejectsAStepCountThatIsNotAWholeNumber() =
        let harness = bare ()

        StringAssert.Contains(harness.Error "progress -steps", "must be a whole number")

    /// There are two numbers, and the message used to blame `steps` for either.
    [<TestMethod>]
    member _.ProgressNamesTheParameterThatIsNotWhole() =
        let harness = bare ()

        Assert.AreEqual<string>("'delay' must be a whole number, not '1.5'.", harness.Error "progress 2 1.5")

    /// A negative delay used to reach the runtime, which threw on most of them — an
    /// `Internal` fault for a user's mistake — and waited for ever on -1.
    [<DataTestMethod>]
    [<DataRow("-5")>]
    [<DataRow("-1")>]
    member _.ProgressRefusesANegativeDelay(delay: string) =
        let harness = bare ()

        let fault = harness.Fail $"progress 2 {delay}"

        Assert.AreEqual<FaultKind>(Invalid, fault.Kind)
        Assert.AreEqual<string>($"'delay' must be zero or more, not '{delay}'.", fault.Message)

    [<TestMethod>]
    member _.ProgressRefusesNegativeSteps() =
        let harness = bare ()

        StringAssert.Contains(harness.Error "progress -3 1", "at least 1")

    [<TestMethod>]
    member _.ProgressCanBeCancelledAndSaysWhereItStopped() =
        let harness = bare ()
        use cancellation = new CancellationTokenSource()
        let run = Async.StartAsTask(harness.Session.Execute("progress 1000 20", 0, cancellation.Token))

        Thread.Sleep 150
        cancellation.Cancel()

        let response = run.Result

        Assert.AreEqual<FaultKind option>(Some Cancelled, response.Fault |> Option.map (fun f -> f.Kind))
        Assert.AreEqual<string option>(Some "Stopped.", response.Fault |> Option.map (fun f -> f.Message))

        Assert.IsTrue(
            response.Output |> List.exists (fun line -> line.StartsWith "Cancelled at"),
            String.Join(" | ", response.Output)
        )

    /// A cancelled line commits nothing, the same as any other failed line.
    [<TestMethod>]
    member _.ACancelledLineLeavesNothingBehind() =
        let harness = bare ()
        use cancellation = new CancellationTokenSource()

        let run =
            Async.StartAsTask(harness.Session.Execute("mkdir a | progress 1000 20", 0, cancellation.Token))

        Thread.Sleep 150
        cancellation.Cancel()
        run.Result |> ignore

        Assert.IsFalse(harness.Exists "a")

    [<TestMethod>]
    member _.ProgressResultFlowsIntoAPipe() =
        let harness = bare ()
        harness.Run "progress 2 1 | set done" |> ignore

        Assert.AreEqual<Value option>(Some(Value.Number 100.0), harness.Variable "done")

    // -------------------------------------------------------------------- download

    [<TestMethod>]
    member _.DownloadRejectsAnInvalidUrl() =
        let harness = bare ()

        StringAssert.Contains(harness.Error "download not-a-url", "Not a valid URL")

    [<TestMethod>]
    member _.DownloadRejectsAMissingTargetDirectory() =
        let harness = bare ()

        StringAssert.Contains(
            harness.Error "download https://example.com/file.txt nowhere",
            "Target directory does not exist"
        )

    /// The downloaded file is a record like any other, so it is undone by the same
    /// mechanism as everything else rather than by a method of its own.
    [<TestMethod>]
    member _.DownloadWritesTheBodyToARecord() =
        let harness =
            Harness(Seed.standardFiles, (fun () -> new HttpClient(new StubHandler("downloaded body"))))

        harness.Run "download https://example.com/file.txt" |> ignore

        Assert.AreEqual<string>("downloaded body", harness.Content "file.txt")

        harness.Run "undo" |> ignore
        Assert.IsFalse(harness.Exists "file.txt")

    /// A download over an existing file is a write, and emits what `write` emits: new
    /// content and a touched `modified`. It used to change the content alone.
    [<TestMethod>]
    member _.DownloadOverAFileChangesWhatWriteChanges() =
        let harness =
            Harness(Seed.standardFiles, (fun () -> new HttpClient(new StubHandler("downloaded body"))))

        let kinds () =
            harness.Session.History()
            |> List.last
            |> fun entry -> entry.Transaction.Events
            |> List.map (fun event ->
                match event with
                | ContentChanged _ -> "content"
                | AttributesChanged _ -> "attributes"
                | other -> sprintf "%A" other)

        harness.Run "write readme.txt written" |> ignore
        let written = kinds ()
        harness.Run "download https://example.com/readme.txt" |> ignore

        Assert.AreEqual<string list>([ "content"; "attributes" ], written)
        Assert.AreEqual<string list>(written, kinds ())
        Assert.AreEqual<string>("downloaded body", harness.Content "readme.txt")
