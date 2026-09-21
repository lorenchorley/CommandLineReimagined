using CommandLineReimagined.Web;

namespace Web.Core.Tests;

/// <summary>
/// The session the browser talks to: what it answers, what it streams while a command
/// runs, how it stops one, and what it offers as completions.
/// </summary>
[TestClass]
public class TerminalSessionTests
{
    private string _root = string.Empty;
    private TerminalSession _session = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "clr-web-" + Guid.NewGuid().ToString("N"));
        _session = new TerminalSession(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    // ---- responses --------------------------------------------------------------

    [TestMethod]
    public async Task ListingReturnsChipsForTheSeededTree()
    {
        var response = await _session.ExecuteAsync("ls");

        Assert.IsNull(response.Error);
        var kinds = response.Result!.Select(item => (item.Kind, item.Text)).ToList();
        CollectionAssert.Contains(kinds, ("directory", "documents"));
        CollectionAssert.Contains(kinds, ("file", "readme.txt"));
    }

    [TestMethod]
    public async Task ReadingAFileReturnsAText()
    {
        var response = await _session.ExecuteAsync("cat readme.txt");

        Assert.IsNull(response.Error);
        Assert.AreEqual("text", response.Result!.Single().Kind);
        StringAssert.Contains(response.Result![0].Text, "browser tab");
    }

    [TestMethod]
    public async Task NumbersAndComponentsHaveTheirOwnKinds()
    {
        Assert.AreEqual("number", (await _session.ExecuteAsync("echo 3")).Result!.Single().Kind);
        Assert.AreEqual("component", (await _session.ExecuteAsync("{renderer/}")).Result!.Single().Kind);
    }

    [TestMethod]
    public async Task ASyntaxErrorNamesTheColumnAndWhatWasExpected()
    {
        var response = await _session.ExecuteAsync("<thing");

        StringAssert.Contains(response.Error, "Syntax error at column 6");
        StringAssert.Contains(response.Error, "expected");
    }

    // A mismatched closing tag arrives as a message, not a position. It used to render
    // as "error error at column 0", which threw away the only useful part.
    [TestMethod]
    public async Task AMismatchedClosingTagIsExplained()
    {
        var response = await _session.ExecuteAsync("<a></b>");

        StringAssert.Contains(response.Error, "does not match opening tag");
    }

    [TestMethod]
    public async Task ACommandErrorIsReturnedNotThrown()
    {
        var response = await _session.ExecuteAsync("cd nowhere");

        StringAssert.Contains(response.Error, "Directory does not exist");
    }

    [TestMethod]
    public async Task WritingThenUndoingLeavesTheTreeAsItWas()
    {
        await _session.ExecuteAsync("echo hi | write note.txt");
        Assert.IsTrue(File.Exists(Path.Combine(_root, "note.txt")));

        var undo = _session.Undo();

        Assert.IsNull(undo.Error);
        Assert.IsFalse(File.Exists(Path.Combine(_root, "note.txt")));
    }

    [TestMethod]
    public async Task UndoNamesTheCommandItReversed()
    {
        await _session.ExecuteAsync("mkdir alpha");

        CollectionAssert.Contains(_session.Undo().Output.ToList(), "Undone: mkdir");
    }

    [TestMethod]
    public void UndoWithAnEmptyHistorySaysSo() =>
        Assert.AreEqual("Nothing to undo.", _session.Undo().Error);

    // Commands keep their undo state in their own fields, so a shared instance made the
    // second undo replay the first one's saved state and leave $v bound to 1.
    [TestMethod]
    public async Task UndoingTwoInvocationsOfOneCommandUnwindsBoth()
    {
        await _session.ExecuteAsync("set v 1");
        await _session.ExecuteAsync("set v 2");

        _session.Undo();
        Assert.AreEqual("1", _session.Variables().Single().Text);

        _session.Undo();
        Assert.AreEqual(0, _session.Variables().Count);
    }

    [TestMethod]
    public async Task UndoingTwoDirectoriesRemovesBoth()
    {
        await _session.ExecuteAsync("mkdir one");
        await _session.ExecuteAsync("mkdir two");

        _session.Undo();
        _session.Undo();

        Assert.IsFalse(Directory.Exists(Path.Combine(_root, "one")));
        Assert.IsFalse(Directory.Exists(Path.Combine(_root, "two")));
    }

    // Read-only commands are on the history too, so undo steps over them one at a time.
    [TestMethod]
    public async Task UndoStepsBackOverACommandThatChangedNothing()
    {
        await _session.ExecuteAsync("mkdir alpha");
        await _session.ExecuteAsync("ls");

        CollectionAssert.Contains(_session.Undo().Output.ToList(), "Undone: ls");
        Assert.IsTrue(Directory.Exists(Path.Combine(_root, "alpha")));

        CollectionAssert.Contains(_session.Undo().Output.ToList(), "Undone: mkdir");
        Assert.IsFalse(Directory.Exists(Path.Combine(_root, "alpha")));
    }

    // ---- streaming and cancellation --------------------------------------------

    [TestMethod]
    public async Task OutputIsRaisedWhileACommandRuns()
    {
        var seen = new List<(int Id, IReadOnlyList<string> Lines)>();
        _session.OutputChanged += (id, lines) => seen.Add((id, lines.ToList()));

        var response = await _session.ExecuteAsync("progress 3 1", executionId: 7);

        Assert.IsNull(response.Error);
        Assert.IsTrue(seen.Count >= 3, $"Expected several updates, saw {seen.Count}.");
        Assert.IsTrue(seen.All(s => s.Id == 7));
        Assert.IsTrue(seen.Any(s => s.Lines.Contains("100%")));
        CollectionAssert.Contains(response.Output.ToList(), "Progress test finished");
    }

    [TestMethod]
    public async Task CancelStopsTheRunningCommand()
    {
        var run = _session.ExecuteAsync("progress 1000 20");
        await Task.Delay(100);

        Assert.IsTrue(_session.IsRunning);
        Assert.IsTrue(_session.Cancel());

        var response = await run;

        Assert.AreEqual("Stopped.", response.Error);
        Assert.IsTrue(response.Output.Any(line => line.StartsWith("Cancelled at")), string.Join("|", response.Output));
        Assert.IsFalse(_session.IsRunning);
    }

    [TestMethod]
    public void CancelWithNothingRunningSaysSo() => Assert.IsFalse(_session.Cancel());

    [TestMethod]
    public async Task ASecondCommandIsRefusedWhileOneRuns()
    {
        var run = _session.ExecuteAsync("progress 1000 20");
        await Task.Delay(50);

        var second = await _session.ExecuteAsync("ls");

        StringAssert.Contains(second.Error, "already running");

        _session.Cancel();
        await run;
    }

    // ---- tokens -----------------------------------------------------------------

    [TestMethod]
    public void TheFunctionFormsNameIsTokenisedAsACommand()
    {
        var parse = new CommandLineReimagined.Web.Parsing.CommandParseService().Parse("write(note.txt, hi)");

        Assert.IsNull(parse.Error);
        Assert.AreEqual(("write", "command"), (parse.Tokens[0].Text, parse.Tokens[0].Kind));
        Assert.AreEqual("note.txt", parse.Tokens.First(t => t.Kind == "identifier").Text);
    }

    [TestMethod]
    public void ParsingRoundTripsTheSourceText()
    {
        var parse = new CommandLineReimagined.Web.Parsing.CommandParseService().Parse("ls | set files");

        Assert.AreEqual("ls | set files", parse.Reserialised);
    }

    // ---- completion -------------------------------------------------------------

    [TestMethod]
    public void TheFirstWordCompletesToCommandNames()
    {
        var texts = _session.Complete("c").Select(c => c.Text).ToList();

        CollectionAssert.Contains(texts, "cat");
        CollectionAssert.Contains(texts, "cd");
        CollectionAssert.Contains(texts, "cp");
        CollectionAssert.Contains(texts, "clear");
        Assert.IsTrue(_session.Complete("c").All(c => c.Kind == "command" && c.Start == 0));
    }

    [TestMethod]
    public void AnArgumentCompletesToEntriesInTheCurrentDirectory()
    {
        var completions = _session.Complete("cat re");

        var only = completions.Single();
        Assert.AreEqual("file", only.Kind);
        Assert.AreEqual("readme.txt", only.Text);
        Assert.AreEqual(4, only.Start);
    }

    [TestMethod]
    public void DirectoriesCompleteWithATrailingSeparatorAndDescend()
    {
        var top = _session.Complete("cd doc").Single();
        Assert.AreEqual("documents/", top.Text);
        Assert.AreEqual("directory", top.Kind);

        var inside = _session.Complete("cat documents/no").Single();
        Assert.AreEqual("documents/notes.txt", inside.Text);
    }

    [TestMethod]
    public async Task ADollarCompletesToVariables()
    {
        await _session.ExecuteAsync("set greeting hello");
        await _session.ExecuteAsync("set grade 1");

        var texts = _session.Complete("echo $gr").Select(c => c.Text).ToList();

        CollectionAssert.AreEquivalent(new[] { "$grade", "$greeting" }, texts);
        Assert.AreEqual(5, _session.Complete("echo $gr")[0].Start);
    }

    [TestMethod]
    public void TheWordAfterAPipeCompletesToCommands()
    {
        var texts = _session.Complete("ls | se").Select(c => c.Text).ToList();

        CollectionAssert.AreEqual(new[] { "set" }, texts);
    }

    [TestMethod]
    public void AnEmptyLineOffersNothing() => Assert.AreEqual(0, _session.Complete("").Count);

    [TestMethod]
    public async Task VariablesAreListedForTheClient()
    {
        await _session.ExecuteAsync("set n 5");

        var variable = _session.Variables().Single();

        Assert.AreEqual("n", variable.Name);
        Assert.AreEqual("5", variable.Text);
        Assert.AreEqual("number", variable.Items.Single().Kind);
    }
}
