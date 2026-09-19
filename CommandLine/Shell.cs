using CommandLine.Modules;
using Commands;
using Commands.Parser;
using Commands.Parser.SemanticTree;
using UIComponents.Components;
using EntityComponentSystem;
using InteractionLogic;
using OneOf;
using Terminal.Execution;
using Terminal.Scoping;
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
/// The shell now only parses, delegates to <see cref="CommandEvaluator"/>, and renders
/// what comes back. Argument conversion, pipes and command dispatch moved into the
/// execution layer, where they can be tested without a scene.
/// </remarks>
public class Shell : IECSSubsystem
{
    private readonly List<CommandDefinition> _commandProfiles;
    private readonly CommandLineReimagined.Parsing.CommandLineParser _interpreter;
    private readonly ConsoleOutModule _consoleOutModule;
    private readonly Prompt _prompt;
    private readonly CommandSearch _commandSearch;
    private readonly ITextUpdateSystem _textUpdateSystem;
    private readonly Scene _scene;
    private readonly ECS _ecs;
    private readonly CommandEvaluator _evaluator;
    private readonly ResultRenderer _renderer;
    private readonly ScopeRegistry _scopeRegistry;

    public Shell(ECS ecs,
                 IEnumerable<ICommandAction> commandActions,
                 ConsoleOutModule consoleOutModule,
                 Prompt prompt,
                 CommandSearch commandSearch,
                 ITextUpdateSystem textUpdateSystem,
                 Scene sceneSetup,
                 CommandEvaluator evaluator,
                 ResultRenderer renderer,
                 ScopeRegistry scopeRegistry)
    {
        _commandProfiles = commandActions.Select(c => c.Profile).ToList();
        _ecs = ecs;
        _consoleOutModule = consoleOutModule;
        _prompt = prompt;
        _commandSearch = commandSearch;
        _textUpdateSystem = textUpdateSystem;
        _scene = sceneSetup;
        _evaluator = evaluator;
        _renderer = renderer;
        _scopeRegistry = scopeRegistry;
        _interpreter = new CommandLineReimagined.Parsing.CommandLineParser();
    }

    public void OnInit()
    {
        _commandSearch.AsynchronouslyLoadIndexes();
    }

    public void OnStart()
    {
    }

    public void RegisterCommand(CommandDefinition commandProfile)
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

        // Fire and forget so a long-running command does not block the input thread,
        // but errors are reported rather than discarded, which is what the previous
        // catch-everything-and-return-false did.
        _ = ExecuteAsync(parsedCommand, block);

        _textUpdateSystem.ClearText();
    }

    private async Task ExecuteAsync(RootNode parsedCommand, CliBlock block)
    {
        try
        {
            var result = await _evaluator.ExecuteAsync(
                parsedCommand, block, _scopeRegistry.Global, CancellationToken.None);

            _renderer.Render(result, block);
        }
        catch (ConsoleError error)
        {
            _renderer.RenderError(error.Message, block);
        }
        catch (OperationCanceledException)
        {
            _renderer.RenderError("Cancelled.", block);
        }
        catch (Exception exception)
        {
            // Still caught, so one bad command cannot take the shell down, but the
            // message now reaches the user instead of vanishing.
            _renderer.RenderError($"{exception.GetType().Name} : {exception.Message}", block);
        }
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
