using Terminal.Execution;

namespace Execution.Tests;

/// <summary>
/// Async commands run without a render loop: progress is written to the output sink and
/// cancellation goes through the invocation's token.
/// </summary>
[TestClass]
public class AsyncCommandTests
{
    private string _root = string.Empty;
    private TestHarness _harness = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "clr-exec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _harness = new TestHarness(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProgressRunsToCompletionAndReturnsThePercentage()
    {
        var result = await _harness.RunAsync("progress 4 1");

        Assert.AreEqual(100d, ((NumberValue)result).Number);
        Assert.IsTrue(_harness.Output.Written.Contains("100%"));
        Assert.IsTrue(_harness.Output.Written.Contains("Progress test finished"));
    }

    [TestMethod]
    public async Task ProgressAcceptsNamedArguments()
    {
        var result = await _harness.RunAsync("progress -steps 2 -delay 1");

        Assert.AreEqual(100d, ((NumberValue)result).Number);
    }

    [TestMethod]
    public void ProgressRejectsZeroSteps() =>
        StringAssert.Contains(_harness.RunExpectingError("progress 0 1"), "at least 1");

    [TestMethod]
    public async Task ProgressCanBeCancelledAndSaysWhereItStopped()
    {
        using var cancellation = new CancellationTokenSource();
        var run = _harness.RunAsync("progress 1000 20", cancellation.Token);

        await Task.Delay(120);
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => run);
        Assert.IsTrue(_harness.Output.Written.Any(line => line.StartsWith("Cancelled at")),
            string.Join(" | ", _harness.Output.Written));
    }

    [TestMethod]
    public async Task ProgressResultFlowsIntoAPipe()
    {
        var result = await _harness.RunAsync("progress 2 1 | set done");

        Assert.AreEqual(100d, ((NumberValue)_harness.Scope.GetVariable("done")!.Value).Number);
        Assert.AreEqual(result, _harness.Scope.GetVariable("done")!.Value);
    }

    [TestMethod]
    public void DownloadRejectsAnInvalidUrl() =>
        StringAssert.Contains(_harness.RunExpectingError("download not-a-url"), "Not a valid URL");

    [TestMethod]
    public void DownloadRejectsAMissingTargetDirectory() =>
        StringAssert.Contains(
            _harness.RunExpectingError("download https://example.com/file.txt nowhere"),
            "Target directory does not exist");
}
