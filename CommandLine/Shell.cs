using CommandLine.Modules;
using CommandLineReimagined.Core;
using Commands.Parser;
using Commands.Parser.SemanticTree;
using UIComponents.Components;
using EntityComponentSystem;
using InteractionLogic;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using OneOf;
using Terminal.Execution;
using Terminal.Search;

namespace Terminal;

[GenerateOneOf]
public partial class CommandAnalysisResult : OneOfBase<CommandPassedChecks, ParserError, CommandFailedTypeChecking>
{

}

public enum ParsingErrorType
{
    Unknown,
    SyntaxError,
    LexicalError
}

public class CommandPassedChecks
{
    // TODO Break up the tree into a series of tokens that can be easily converted into line segments
    // Include mouse hover info, types, etc. enough that the text can be interacted with and coloured appropriately
    public CommandPassedChecks(RootNode tree)
    {
    }
}

public class CommandFailedTypeChecking
{

}

/// <summary>
/// Turns what is typed at the prompt into an execution.
/// </summary>
/// <remarks>
/// The shell parses, hands the line to the F# <see cref="Session"/>, and renders what
/// comes back. Everything about what a command means lives in the core, which is what
/// lets the same semantics run here and in a browser tab.
///
/// Rendering is text for this phase. The interactive result buttons that `ls` used to
/// draw -- context menus, double-click actions -- are a desktop affordance built on
/// path values, and rebuilding them on file records is work this phase does not need
/// to do to prove the core. Decision 0012 puts the desktop shell out of scope; the
/// requirement here is that it still compiles and still runs commands.
/// </remarks>
public class Shell : IECSSubsystem
{
    private readonly List<CommandSpec> _commandProfiles;
    private readonly CommandLineReimagined.Parsing.CommandLineParser _interpreter;
    private readonly ConsoleOutModule _consoleOutModule;
    private readonly Prompt _prompt;
    private readonly CommandSearch _commandSearch;
    private readonly ITextUpdateSystem _textUpdateSystem;
    private readonly Scene _scene;
    private readonly ECS _ecs;
    private readonly Session _session;
    private readonly ResultRenderer _renderer;
    private int _nextExecutionId = 1;

    public Shell(ECS ecs,
                 ConsoleOutModule consoleOutModule,
                 Prompt prompt,
                 CommandSearch commandSearch,
                 ITextUpdateSystem textUpdateSystem,
                 Scene sceneSetup,
                 Session session,
                 ResultRenderer renderer)
    {
        _session = session;
        _commandProfiles = session.Commands.ToList();
        _ecs = ecs;
        _consoleOutModule = consoleOutModule;
        _prompt = prompt;
        _commandSearch = commandSearch;
        _textUpdateSystem = textUpdateSystem;
        _scene = sceneSetup;
        _renderer = renderer;
        _interpreter = new CommandLineReimagined.Parsing.CommandLineParser();
    }

    public void OnInit()
    {
        // The log has to be replayed before anything can run. It is in memory here, so
        // it completes at once; the await is what will make a persisted log work.
        _ = InitialiseAsync();
        _commandSearch.AsynchronouslyLoadIndexes();
    }

    private async Task InitialiseAsync() =>
        await FSharpAsync.StartAsTask(
            _session.Initialize(),
            FSharpOption<TaskCreationOptions>.None,
            FSharpOption<CancellationToken>.None);

    public void OnStart()
    {
    }

    public void RegisterCommand(CommandSpec commandProfile)
    {
        _commandProfiles.Add(commandProfile);
    }

    public CommandAnalysisResult AnalyseCommand(string command)
    {
        ParserResult<RootNode> result = _interpreter.Parse<RootNode>(command);

        return result.Match<CommandAnalysisResult>(
            tree => new CommandPassedChecks(tree),
            errors => errors
        );
    }

    public void ExecuteCurrentPrompt()
    {
        if (!_prompt.TryGetValidCommand(out RootNode? parsedCommand, out string? commandText))
        {
            return;
        }

        var block = _consoleOutModule.StartBlock(commandText);

        EchoPrompt(block);

        // Fire and forget so a long-running command does not block the input thread.
        // The session never raises, so there is nothing here to catch: every failure
        // comes back as a fault in the response.
        _ = ExecuteAsync(commandText, block);

        _textUpdateSystem.ClearText();
    }

    /// <summary>
    /// Runs a line that nobody typed, such as the one behind the undo key.
    /// </summary>
    /// <remarks>
    /// It goes through exactly the path a typed line does, block and all, so the key
    /// and the word cannot drift apart and what it did shows up in the console.
    /// </remarks>
    public void ExecuteLine(string commandText)
    {
        var block = _consoleOutModule.StartBlock(commandText);
        block.NewLineComponent().LinkNewTextBlock("echo", commandText);
        _ = ExecuteAsync(commandText, block);
    }

    /// <summary>
    /// Runs a line and renders what comes back.
    /// </summary>
    /// <remarks>
    /// The line is handed over as text rather than as the tree the prompt parsed,
    /// because a transaction is named by its source and that name is what `history`
    /// and `Undone:` show the user. The session parses it again, which costs a
    /// microsecond and keeps one answer to what a line says.
    ///
    /// Nothing is caught. <c>Session.Execute</c> does not raise: a parse failure, a
    /// cancellation and an unexpected exception are all faults in the response. That
    /// is decision 0006 from the outside.
    /// </remarks>
    private async Task ExecuteAsync(string commandText, CliBlock block)
    {
        var response = await FSharpAsync.StartAsTask(
            _session.Execute(commandText, _nextExecutionId++, CancellationToken.None),
            FSharpOption<TaskCreationOptions>.None,
            FSharpOption<CancellationToken>.None);

        _renderer.Render(response, block);
    }

    private void EchoPrompt(CliBlock block)
    {
        var promptLine = block.NewLineComponent();
        var promptText = _ecs.NewEntity("Prompt").AddComponent<TextComponent>();

        promptText.Text =
            _scene.InputPanel
                  .Lines
                  .SelectMany(line => line.LineSegments)
                  .OfType<TextComponent>()
                  .FirstOrDefault()
                  ?.ToText()
                  ?? "";

        promptLine.AddLineSegment(promptText);
    }
}
