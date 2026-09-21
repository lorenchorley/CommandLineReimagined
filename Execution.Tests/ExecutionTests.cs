using Terminal.Execution;

namespace Execution.Tests;

[TestClass]
public class ExecutionTests
{
    private string _root = string.Empty;
    private TestHarness _harness = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "clr-exec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "documents"));
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

    // ---- argument binding -------------------------------------------------------
    // The original converter recognised only StringConstant, so every unquoted
    // argument threw NotImplementedException and was then silently swallowed.

    [TestMethod]
    public void UnquotedArgumentBinds()
    {
        var result = _harness.Run("cd documents");

        Assert.IsInstanceOfType(result, typeof(PathValue));
        StringAssert.EndsWith(_harness.PathModule.CurrentPath.TrimEnd(Path.DirectorySeparatorChar), "documents");
    }

    [TestMethod]
    public void QuotedArgumentBinds()
    {
        _harness.Run(@"cd ""documents""");

        StringAssert.EndsWith(_harness.PathModule.CurrentPath.TrimEnd(Path.DirectorySeparatorChar), "documents");
    }

    [TestMethod]
    public void NumericArgumentBindsAsNumber()
    {
        var result = _harness.Run("echo 42");

        Assert.IsInstanceOfType(result, typeof(NumberValue));
        Assert.AreEqual(42d, ((NumberValue)result).Number);
    }

    [TestMethod]
    public void MissingRequiredArgumentIsReported()
    {
        string message = _harness.RunExpectingError("cd");

        StringAssert.Contains(message, "needs an argument");
        StringAssert.Contains(message, "TargetPath");
    }

    [TestMethod]
    public void TooManyArgumentsAreReported()
    {
        string message = _harness.RunExpectingError("cd documents extra");

        // Counts what was supplied rather than what was left over: the old wording read
        // "takes 1 argument(s), but 1 more were given" for a two-argument call.
        StringAssert.Contains(message, "takes 1 argument, but 2 were given");
    }

    [TestMethod]
    public void UnknownNamedArgumentIsReported()
    {
        string message = _harness.RunExpectingError("ls -nonsense value");

        StringAssert.Contains(message, "no argument named");
    }

    [TestMethod]
    public void NamedArgumentBindsOutOfPosition()
    {
        var result = _harness.Run("ls -path documents");

        Assert.IsInstanceOfType(result, typeof(ListValue));
    }

    // ---- pipes ------------------------------------------------------------------
    // ExecuteNominal used to run OrderedCommands.First() and discard the rest.

    [TestMethod]
    public void PipeRunsEveryStage()
    {
        var result = _harness.Run("mkdir alpha | echo");

        // echo receives mkdir's result rather than re-running mkdir.
        Assert.IsInstanceOfType(result, typeof(PathValue));
        Assert.IsTrue(Directory.Exists(Path.Combine(_root, "alpha")));
        StringAssert.EndsWith(((PathValue)result).Path.TrimEnd(Path.DirectorySeparatorChar), "alpha");
    }

    [TestMethod]
    public void PipeThreadsValueBetweenCommands()
    {
        var result = _harness.Run("echo documents | cd");

        Assert.IsInstanceOfType(result, typeof(PathValue));
        StringAssert.EndsWith(_harness.PathModule.CurrentPath.TrimEnd(Path.DirectorySeparatorChar), "documents");
    }

    [TestMethod]
    public void LaterPipeStageFailureIsReported()
    {
        string message = _harness.RunExpectingError("echo nowhere | cd");

        StringAssert.Contains(message, "Directory does not exist");
    }

    // ---- command forms ----------------------------------------------------------
    // Only the CLI form executed before; the other two threw NotImplementedException.

    [TestMethod]
    public void FunctionFormExecutes()
    {
        var result = _harness.Run("echo(hello)");

        Assert.AreEqual("hello", result.ToDisplayString());
    }

    [TestMethod]
    public void FunctionFormWithNamedArgumentExecutes()
    {
        var result = _harness.Run("echo(text: hello)");

        Assert.AreEqual("hello", result.ToDisplayString());
    }

    [TestMethod]
    public void ObjectInstanceFormEvaluates()
    {
        var result = _harness.Run("<measurement unit=metres/>");

        var instance = (ObjectValue)result;
        Assert.AreEqual("measurement", instance.TypeName);
        Assert.AreEqual("metres", instance.Attributes["unit"].ToDisplayString());
    }

    [TestMethod]
    public void NestedObjectInstanceEvaluates()
    {
        var result = _harness.Run("<outer><inner depth=2/></outer>");

        var instance = (ObjectValue)result;
        Assert.AreEqual(1, instance.Children.Count);
        Assert.AreEqual("inner", instance.Children[0].TypeName);
        Assert.AreEqual(2d, ((NumberValue)instance.Children[0].Attributes["depth"]).Number);
    }

    // ---- variables --------------------------------------------------------------
    // Variable had get-only properties and no constructor, so nothing could be bound.

    [TestMethod]
    public void ObjectInstanceBindsVariable()
    {
        _harness.Run("<size|measurement unit=metres/>");

        var variable = _harness.Scope.GetVariable("size");
        Assert.IsNotNull(variable);
        Assert.AreEqual("measurement", ((ObjectValue)variable!.Value).TypeName);
    }

    [TestMethod]
    public void VariableReferenceResolvesAsArgument()
    {
        _harness.Run("<target|folder name=documents/>");
        var result = _harness.Run("echo $target");

        Assert.IsInstanceOfType(result, typeof(ObjectValue));
    }

    [TestMethod]
    public void UnknownVariableIsReported()
    {
        string message = _harness.RunExpectingError("echo $missing");

        StringAssert.Contains(message, "Unknown variable");
    }

    // ---- tags as arguments --------------------------------------------------------
    // The grammar admits a tag wherever an argument is expected. Only a tag standing
    // alone as a pipeline stage used to evaluate; everywhere else reported
    // "Unsupported argument value : TagValue".

    [TestMethod]
    public void TagIsAcceptedAsAnArgument()
    {
        var result = (ObjectValue)_harness.Run("echo <measurement unit=metres/>");

        Assert.AreEqual("measurement", result.TypeName);
        Assert.AreEqual("metres", result.Attributes["unit"].ToDisplayString());
    }

    [TestMethod]
    public void ComponentTagIsAcceptedAsAnArgument() =>
        Assert.IsInstanceOfType(_harness.Run("echo {renderer/}"), typeof(ComponentValue));

    [TestMethod]
    public void TagAsAnArgumentStillBindsItsVariable()
    {
        _harness.Run("echo <size|measurement unit=metres/>");

        Assert.IsInstanceOfType(_harness.Scope.GetVariable("size")!.Value, typeof(ObjectValue));
    }

    [TestMethod]
    public void TagIsAcceptedAsAFunctionArgument() =>
        Assert.IsInstanceOfType(_harness.Run("echo(<thing/>)"), typeof(ObjectValue));

    [TestMethod]
    public void VariableTagIsAcceptedAsAnArgument()
    {
        _harness.Run("<size|measurement unit=metres/>");

        Assert.AreEqual("measurement", ((ObjectValue)_harness.Run("echo <$size>")).TypeName);
    }

    // ---- errors -----------------------------------------------------------------

    [TestMethod]
    public void UnknownCommandIsReported()
    {
        string message = _harness.RunExpectingError("nosuchcommand");

        StringAssert.Contains(message, "Unknown command");
    }

    [TestMethod]
    public void FailingCommandReportsWhyRatherThanSilentlyDoingNothing()
    {
        string message = _harness.RunExpectingError("cd nowhere");

        StringAssert.Contains(message, "Directory does not exist");
    }

    // ---- results ----------------------------------------------------------------

    [TestMethod]
    public void ListDirectoryReturnsEntries()
    {
        var result = (ListValue)_harness.Run("ls");

        var paths = result.Items.Cast<PathValue>().ToList();
        Assert.IsTrue(paths.Any(p => p.Kind == PathKind.Directory && p.Name == "documents"));
        Assert.IsTrue(paths.Any(p => p.Kind == PathKind.File && p.Name == "notes.txt"));
    }

    [TestMethod]
    public void EmptyCommandDoesNothing()
    {
        var result = _harness.Run("");

        Assert.AreSame(RuntimeValue.Empty, result);
    }

    // ---- component tags ----------------------------------------------------------
    // The parser builds these now, so the evaluator has to produce a value for them.

    [TestMethod]
    public void ComponentTagEvaluates()
    {
        var component = (ComponentValue)_harness.Run("{renderer colour=red/}");

        Assert.AreEqual("renderer", component.TypeName);
        Assert.AreEqual("red", component.Attributes["colour"].ToDisplayString());
    }

    [TestMethod]
    public void ComponentTagBindsAVariable()
    {
        _harness.Run("{handle|renderer/}");

        Assert.IsInstanceOfType(_harness.Scope.GetVariable("handle")!.Value, typeof(ComponentValue));
    }

    [TestMethod]
    public void ComponentReadsBackTheWayItWasWritten() =>
        Assert.AreEqual("{renderer colour=red/}", _harness.Run("{renderer colour=red/}").ToDisplayString());

    [TestMethod]
    public void EntityMayContainAComponent()
    {
        var entity = (ObjectValue)_harness.Run("<player>{renderer/}</player>");

        Assert.AreEqual("player", entity.TypeName);
    }

    [TestMethod]
    public void VariableTagReadsTheVariableBack()
    {
        _harness.Run("<size|measurement unit=metres/>");
        var value = (ObjectValue)_harness.Run("<$size>");

        Assert.AreEqual("measurement", value.TypeName);
    }

    [TestMethod]
    public void VariableTagForAnUnknownNameIsReported() =>
        StringAssert.Contains(_harness.RunExpectingError("<$missing>"), "Unknown variable");

    // ---- undo -------------------------------------------------------------------

    [TestMethod]
    public void UndoReversesDirectoryCreation()
    {
        _harness.Run("mkdir beta");
        Assert.IsTrue(Directory.Exists(Path.Combine(_root, "beta")));

        _harness.History.UndoLast();

        Assert.IsFalse(Directory.Exists(Path.Combine(_root, "beta")));
    }

    [TestMethod]
    public void UndoReversesDirectoryChange()
    {
        string before = _harness.PathModule.CurrentPath;
        _harness.Run("cd documents");

        _harness.History.UndoLast();

        Assert.AreEqual(before, _harness.PathModule.CurrentPath);
    }

    [TestMethod]
    public void UndoClearsTheCommandsOutput()
    {
        _harness.Run("mkdir gamma");

        _harness.History.UndoLast();

        Assert.IsTrue(_harness.Output.Cleared);
    }

    [TestMethod]
    public void UndoOnEmptyHistoryIsHarmless()
    {
        _harness.History.UndoLast();

        Assert.AreEqual(0, _harness.History.Count);
    }
}
