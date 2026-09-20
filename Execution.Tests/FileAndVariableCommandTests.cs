using Terminal.Execution;

namespace Execution.Tests;

/// <summary>
/// The commands added for editing files and naming values: cat, write, rm, pwd, set,
/// vars. Each one that changes something is also undone.
/// </summary>
[TestClass]
public class FileAndVariableCommandTests
{
    private string _root = string.Empty;
    private TestHarness _harness = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "clr-exec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "documents"));
        Directory.CreateDirectory(Path.Combine(_root, "empty"));
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "hello");

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

    private string At(params string[] parts) => Path.Combine(new[] { _root }.Concat(parts).ToArray());

    // ---- cat --------------------------------------------------------------------

    [TestMethod]
    public void CatReturnsTheFileAsText()
    {
        var result = _harness.Run("cat notes.txt");

        Assert.IsInstanceOfType(result, typeof(TextValue));
        Assert.AreEqual("hello", ((TextValue)result).Text);
    }

    [TestMethod]
    public void CatAcceptsAPipedPath()
    {
        var result = _harness.Run("echo notes.txt | cat");

        Assert.AreEqual("hello", result.ToDisplayString());
    }

    [TestMethod]
    public void CatOfAMissingFileIsReported() =>
        StringAssert.Contains(_harness.RunExpectingError("cat nowhere.txt"), "File does not exist");

    [TestMethod]
    public void CatOfADirectoryIsReported() =>
        StringAssert.Contains(_harness.RunExpectingError("cat documents"), "directory, not a file");

    // ---- write ------------------------------------------------------------------

    [TestMethod]
    public void WriteCreatesAFileAndReturnsItsPath()
    {
        var result = (PathValue)_harness.Run("write new.txt content");

        Assert.AreEqual(PathKind.File, result.Kind);
        Assert.AreEqual("content", File.ReadAllText(At("new.txt")));
    }

    [TestMethod]
    public void WriteTakesItsTextFromThePipe()
    {
        _harness.Run("cat notes.txt | write copy.txt");

        Assert.AreEqual("hello", File.ReadAllText(At("copy.txt")));
    }

    [TestMethod]
    public void WriteIntoAMissingDirectoryIsReported() =>
        StringAssert.Contains(_harness.RunExpectingError("write nowhere/x.txt text"), "Directory does not exist");

    [TestMethod]
    public void UndoOfWriteRestoresThePreviousContents()
    {
        _harness.Run("write notes.txt replaced");
        Assert.AreEqual("replaced", File.ReadAllText(At("notes.txt")));

        _harness.History.UndoLast();

        Assert.AreEqual("hello", File.ReadAllText(At("notes.txt")));
    }

    [TestMethod]
    public void UndoOfWriteRemovesAFileThatDidNotExist()
    {
        _harness.Run("write fresh.txt text");

        _harness.History.UndoLast();

        Assert.IsFalse(File.Exists(At("fresh.txt")));
    }

    // ---- rm ---------------------------------------------------------------------

    [TestMethod]
    public void RmDeletesAFile()
    {
        _harness.Run("rm notes.txt");

        Assert.IsFalse(File.Exists(At("notes.txt")));
    }

    [TestMethod]
    public void RmDeletesAnEmptyDirectory()
    {
        _harness.Run("rm empty");

        Assert.IsFalse(Directory.Exists(At("empty")));
    }

    [TestMethod]
    public void RmRefusesANonEmptyDirectory()
    {
        File.WriteAllText(At("documents", "a.txt"), "a");

        StringAssert.Contains(_harness.RunExpectingError("rm documents"), "not empty");
        Assert.IsTrue(Directory.Exists(At("documents")));
    }

    [TestMethod]
    public void RmOfNothingIsReported() =>
        StringAssert.Contains(_harness.RunExpectingError("rm ghost"), "Nothing exists");

    [TestMethod]
    public void UndoOfRmRestoresTheFileWithItsContents()
    {
        _harness.Run("rm notes.txt");

        _harness.History.UndoLast();

        Assert.AreEqual("hello", File.ReadAllText(At("notes.txt")));
    }

    [TestMethod]
    public void UndoOfRmRestoresTheDirectory()
    {
        _harness.Run("rm empty");

        _harness.History.UndoLast();

        Assert.IsTrue(Directory.Exists(At("empty")));
    }

    // ---- pwd --------------------------------------------------------------------

    [TestMethod]
    public void PwdReturnsTheCurrentDirectory()
    {
        _harness.Run("cd documents");
        var result = (PathValue)_harness.Run("pwd");

        Assert.AreEqual(PathKind.Directory, result.Kind);
        Assert.AreEqual("documents", result.Name);
    }

    // ---- set / vars -------------------------------------------------------------

    [TestMethod]
    public void SetBindsAVariableAndReturnsTheValue()
    {
        var result = _harness.Run("set greeting hello");

        Assert.AreEqual("hello", result.ToDisplayString());
        Assert.AreEqual("hello", _harness.Scope.GetVariable("greeting")!.Value.ToDisplayString());
    }

    [TestMethod]
    public void SetBindsWhatWasPiped()
    {
        _harness.Run("ls | set files");

        Assert.IsInstanceOfType(_harness.Scope.GetVariable("files")!.Value, typeof(ListValue));
    }

    [TestMethod]
    public void SetVariableCanBeReadBack()
    {
        _harness.Run("set n 42");
        var result = _harness.Run("echo $n");

        Assert.AreEqual(42d, ((NumberValue)result).Number);
    }

    [TestMethod]
    public void SetWithoutAValueIsReported() =>
        StringAssert.Contains(_harness.RunExpectingError("set lonely"), "needs an argument for 'value'");

    // `set $x 1` reads $x rather than naming it, the same as everywhere else in the
    // grammar, so the mistake is reported instead of guessed at.
    [TestMethod]
    public void SetWithAVariableReferenceAsTheNameIsReported() =>
        StringAssert.Contains(_harness.RunExpectingError("set $x 1"), "Unknown variable");

    [TestMethod]
    public void UndoOfSetRemovesANewVariable()
    {
        _harness.Run("set temp 1");

        _harness.History.UndoLast();

        Assert.IsNull(_harness.Scope.GetVariable("temp"));
    }

    [TestMethod]
    public void UndoOfSetRestoresThePreviousBinding()
    {
        _harness.Run("set v first");
        _harness.Run("set v second");

        _harness.History.UndoLast();

        Assert.AreEqual("first", _harness.Scope.GetVariable("v")!.Value.ToDisplayString());
    }

    [TestMethod]
    public void VarsListsEveryVariableOnItsOwnLine()
    {
        _harness.Run("set a 1");
        _harness.Run("set b two");

        var result = (ListValue)_harness.Run("vars");

        Assert.AreEqual(2, result.Items.Count);
        CollectionAssert.Contains(_harness.Output.Written.ToList(), "$a = 1");
        CollectionAssert.Contains(_harness.Output.Written.ToList(), "$b = two");
    }

    [TestMethod]
    public void VarsWithNothingBoundSaysSo()
    {
        var result = _harness.Run("vars");

        Assert.AreSame(RuntimeValue.Empty, result);
        Assert.IsTrue(_harness.Output.Written.Any(line => line.Contains("No variables")));
    }
}
