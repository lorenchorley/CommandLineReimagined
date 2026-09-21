using CommandLineReimagined.Web;

namespace Web.Core.Tests;

/// <summary>
/// The session the browser talks to: what it answers, what it streams while a command
/// runs, how it stops one, and what it offers as completions.
/// </summary>
/// <remarks>
/// These test the adapter, not the language: what a command means is settled in
/// `Core.Tests`, and what is checked here is that a value arrives as the shape the
/// page draws. They no longer touch a temporary directory, because the filesystem is
/// a projection now and there is nothing on disk to look at.
/// </remarks>
[TestClass]
public class TerminalSessionTests
{
    private TerminalSession _session = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _session = new TerminalSession();
        await _session.InitializeAsync();
    }

    /// The names a listing shows, which is the cheapest way to ask what exists.
    private async Task<IReadOnlyList<string>> Listing()
    {
        var response = await _session.ExecuteAsync("ls");
        return response.Result!.Select(item => item.Text).ToList();
    }

    // ---- responses --------------------------------------------------------------

    [TestMethod]
    public async Task ListingReturnsChipsForTheSeededTree()
    {
        var response = await _session.ExecuteAsync("ls");

        Assert.IsNull(response.Error);
        var kinds = response.Result!.Select(item => (item.Kind, item.Text)).ToList();
        CollectionAssert.Contains(kinds, ("folder", "documents"));
        CollectionAssert.Contains(kinds, ("file", "readme.txt"));
    }

    /// A chip carries the path it argues, so tapping one inserts something that
    /// resolves rather than a bare name that only works from where you are.
    [TestMethod]
    public async Task AFileChipCarriesItsPath()
    {
        var response = await _session.ExecuteAsync("ls");

        var readme = response.Result!.Single(item => item.Text == "readme.txt");
        Assert.AreEqual("/readme.txt", readme.Path);
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

    /// <summary>The kind travels beside the sentence.</summary>
    /// <remarks>
    /// `error` is still the sentence the page paints red and has not changed. `fault`
    /// is new, and is what lets the page show what sort of failure it was without
    /// parsing the wording.
    /// </remarks>
    [TestMethod]
    public async Task AFailureCarriesItsKindBesideTheMessage()
    {
        var response = await _session.ExecuteAsync("cd nowhere");

        Assert.AreEqual("NotFound", response.Fault!.Kind);
        Assert.AreEqual(response.Error, response.Fault.Message);
        Assert.AreEqual(1, response.Fault.Stage);
    }

    [TestMethod]
    public async Task AFailureInALaterStageSaysWhichOne()
    {
        var response = await _session.ExecuteAsync("echo nowhere | cd");

        Assert.AreEqual(2, response.Fault!.Stage);
    }

    [TestMethod]
    public async Task ASuccessfulLineHasNoFault()
    {
        var response = await _session.ExecuteAsync("echo hello");

        Assert.IsNull(response.Fault);
        Assert.IsNull(response.Error);
    }

    // ---- location ---------------------------------------------------------------

    [TestMethod]
    public async Task TheResponseSaysWhereTheSessionIs()
    {
        Assert.AreEqual("/", (await _session.ExecuteAsync("ls")).Location.Folder);

        var response = await _session.ExecuteAsync("cd documents");

        Assert.AreEqual("/documents", response.Location.Folder);
        Assert.IsNull(response.Location.View);
        Assert.AreEqual("/documents", _session.Location.Folder);
    }

    // ---- undo, redo and history -------------------------------------------------
    // Commands now, not bridge methods, so they go through ExecuteAsync like the rest.

    [TestMethod]
    public async Task WritingThenUndoingLeavesTheTreeAsItWas()
    {
        await _session.ExecuteAsync("echo hi | write note.txt");
        CollectionAssert.Contains((await Listing()).ToList(), "note.txt");

        var undo = await _session.ExecuteAsync("undo");

        Assert.IsNull(undo.Error);
        CollectionAssert.DoesNotContain((await Listing()).ToList(), "note.txt");
    }

    [TestMethod]
    public async Task UndoNamesTheLineItReversed()
    {
        await _session.ExecuteAsync("mkdir alpha");

        var undo = await _session.ExecuteAsync("undo");

        Assert.AreEqual("Undone: mkdir alpha", undo.ResultText);
    }

    /// Nothing went wrong, so it is a result rather than a red line.
    [TestMethod]
    public async Task UndoWithNothingToUndoIsNotAnError()
    {
        var undo = await _session.ExecuteAsync("undo");

        Assert.IsNull(undo.Error);
        Assert.AreEqual("Nothing to undo.", undo.ResultText);
    }

    [TestMethod]
    public async Task RedoPutsBackWhatUndoTookAway()
    {
        await _session.ExecuteAsync("mkdir alpha");
        await _session.ExecuteAsync("undo");

        var redo = await _session.ExecuteAsync("redo");

        Assert.AreEqual("Redone: mkdir alpha", redo.ResultText);
        CollectionAssert.Contains((await Listing()).ToList(), "alpha");
    }

    // The defect decision 0010 was written for: a shared command instance made the
    // second undo replay the first one's saved state and leave $v bound to 1.
    [TestMethod]
    public async Task UndoingTwoInvocationsOfOneCommandUnwindsBoth()
    {
        await _session.ExecuteAsync("set v 1");
        await _session.ExecuteAsync("set v 2");

        await _session.ExecuteAsync("undo");
        Assert.AreEqual("1", _session.Variables().Single().Text);

        await _session.ExecuteAsync("undo");
        Assert.AreEqual(0, _session.Variables().Count);
    }

    [TestMethod]
    public async Task UndoingTwoDirectoriesRemovesBoth()
    {
        await _session.ExecuteAsync("mkdir one");
        await _session.ExecuteAsync("mkdir two");

        await _session.ExecuteAsync("undo");
        await _session.ExecuteAsync("undo");

        var listing = (await Listing()).ToList();
        CollectionAssert.DoesNotContain(listing, "one");
        CollectionAssert.DoesNotContain(listing, "two");
    }

    /// <summary>Undo reaches past a line that changed nothing (decision 0015).</summary>
    /// <remarks>
    /// This reverses the old behaviour, which is what makes it worth a test of its own.
    /// Every executed command used to be on the history, read-only ones included, so
    /// `mkdir alpha`, `ls`, undo answered "Undone: ls" and left the folder in place --
    /// indistinguishable, to the person watching, from undo being broken.
    /// </remarks>
    [TestMethod]
    public async Task UndoReachesPastALineThatChangedNothing()
    {
        await _session.ExecuteAsync("mkdir alpha");
        await _session.ExecuteAsync("ls");

        var undo = await _session.ExecuteAsync("undo");

        Assert.AreEqual("Undone: mkdir alpha", undo.ResultText);
        CollectionAssert.DoesNotContain((await Listing()).ToList(), "alpha");
    }

    [TestMethod]
    public async Task HistoryListsTheLinesThatChangedSomething()
    {
        await _session.ExecuteAsync("mkdir alpha");
        await _session.ExecuteAsync("ls");

        var history = await _session.ExecuteAsync("history");

        Assert.IsTrue(history.Result!.Any(item => item.Text.Contains("mkdir alpha")));
        Assert.IsFalse(history.Result!.Any(item => item.Text.EndsWith(" ls")));
    }

    // ---- initialisation ---------------------------------------------------------

    /// <summary>
    /// Executing before the log has been replayed is a fault, not an empty filesystem.
    /// </summary>
    /// <remarks>
    /// An empty filesystem and a lost one look identical from the page, so a host that
    /// forgets to await initialisation is told rather than quietly shown nothing. It
    /// matters from Phase 2, when replaying actually takes time.
    /// </remarks>
    [TestMethod]
    public async Task ExecutingBeforeInitialisingIsAFault()
    {
        var fresh = new TerminalSession();

        var response = await fresh.ExecuteAsync("ls");

        Assert.AreEqual("Internal", response.Fault!.Kind);
        StringAssert.Contains(response.Error, "not been initialised");
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

    /// Phase 4's live views listen to this; committing is what raises it.
    [TestMethod]
    public async Task TheStoreAnnouncesEveryCommittedLine()
    {
        var seen = new List<long>();
        _session.StoreChanged += seen.Add;

        await _session.ExecuteAsync("mkdir alpha");
        await _session.ExecuteAsync("ls");

        Assert.AreEqual(1, seen.Count, "The ls changed nothing and should not have announced anything.");
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
        Assert.AreEqual("Cancelled", response.Fault!.Kind);
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

    /// An assignment's name is tagged as an attribute: it names data, rather than
    /// being data itself.
    [TestMethod]
    public void AnAssignmentIsTokenisedAsAnAttribute()
    {
        var parse = new CommandLineReimagined.Web.Parsing.CommandParseService().Parse("attr notes.txt tag=work");

        Assert.IsNull(parse.Error);
        Assert.AreEqual("attr notes.txt tag=work", parse.Reserialised);
        Assert.AreEqual("tag", parse.Tokens.First(t => t.Kind == "attribute").Text);
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
        Assert.AreEqual("folder", top.Kind);

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

    // ---- commands ---------------------------------------------------------------

    [TestMethod]
    public void TheCommandListIsWhatHelpShows()
    {
        var names = _session.Commands.Select(c => c.Name).ToList();

        CollectionAssert.Contains(names, "undo");
        CollectionAssert.Contains(names, "redo");
        CollectionAssert.Contains(names, "history");
        CollectionAssert.Contains(names, "attr");
        CollectionAssert.Contains(names, "save");
        CollectionAssert.DoesNotContain(names, "UnknownCommand");
    }
}
