using CommandLine.Modules;
using Commands;
using Commands.Parser;
using Commands.Parser.SemanticTree;
using Console;
using Console.Components;
using EntityComponentSystem;
using Microsoft.Extensions.DependencyInjection;
using OneOf;
using Rendering.Components;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Terminal.Naming;
using Terminal.Search;
using UIComponents.Compoents.Console;

namespace Terminal
{
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
        // Include mouse hover info, types, etc. enough that the text can bei= interacted with and coloured appropriately
        public CommandPassedChecks(RootNode tree)
        {
        }
    }

    public class CommandFailedTypeChecking
    {

    }

    public class Shell
    {
        private readonly List<CommandDefinition> _commandProfiles;
        private readonly CommandLineInterpreter _interpreter;
        private readonly IServiceProvider _serviceProvider;
        private readonly ECS _ecs;
        private readonly CommandHistoryModule _commandHistoryModule;
        private readonly ConsoleOutModule _consoleOutModule;
        private readonly NameResolver _nameResolver;
        private readonly Prompt _prompt;
        private readonly CommandSearch _commandSearch;

        public event EventHandler<EventArgs> OnInit;

        public Shell(IServiceProvider serviceProvider,
                     ECS ecs,
                     IEnumerable<ICommandAction> commandActions,
                     CommandHistoryModule commandHistoryModule,
                     ConsoleOutModule consoleOutModule,
                     NameResolver nameResolver,
                     Prompt prompt,
                     CommandSearch commandSearch)
        {
            _commandProfiles = commandActions.Select(c => c.Profile).ToList();
            _serviceProvider = serviceProvider;
            _ecs = ecs;
            _commandHistoryModule = commandHistoryModule;
            _consoleOutModule = consoleOutModule;
            _nameResolver = nameResolver;
            _prompt = prompt;
            _commandSearch = commandSearch;

            _interpreter = new CommandLineInterpreter();
        }

        public void Init()
        {
            _commandSearch.AsynchronouslyLoadIndexes();
            SetupScene();
            
            OnInit?.Invoke(this, new EventArgs());
        }

        public UICamera Camera { get; private set; }
        public ConsoleOutputPanel Output { get; private set; }
        public ConsoleInputPanel Input { get; private set; }

        private void SetupScene()
        {
            Camera = _ecs.NewEntity("MainCamera").AddComponent<UICamera>();
            ConsoleLayout Layout = _ecs.NewEntity("Layout").AddComponent<ConsoleLayout>();

            Output = _ecs.NewEntity("Output").AddComponent<ConsoleOutputPanel>();
            Output.Entity.Parent = Layout.Entity;
            Input = _ecs.NewEntity("Input").AddComponent<ConsoleInputPanel>();
            Input.Entity.Parent = Layout.Entity;

            _prompt.Input = Input;
            _prompt.Output = Output;
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

        public bool ExecuteCurrentPrompt()
        {
            if (!_prompt.TryGetValidCommand(out RootNode? parsedCommand, out string? commandText))
            {
                // Parser ou type validation errors
                //ShowPropmptErrors(_prompt.GetErrorDetails());

                return false;
            }

            return ExecuteValidCommand(parsedCommand, commandText);
        }

        private bool ExecuteValidCommand(RootNode parsedCommand, string commandText)
        {
            var consoleBlock = _consoleOutModule.StartBlock(commandText);
            try
            {
                ExecuteNominal(parsedCommand, consoleBlock);
            }
            catch (Exception e)
            {
                return false; // Command runtime exception
            }
            finally
            {
                //consoleBlock.Finalise();
            }

            return true;
        }

        private void ExecuteNominal(RootNode result, CliBlock scope)
        {
            if (result is EmptyCommand)
            {
                // Fait rien
                return;
            }

            if (result is not PipedCommandList commands)
            {
                // Erreur terminale
                throw new Exception("Unknown command tree type : " + result.GetType().Name);
            }

            if (commands.OrderedCommands.Count == 0)
            {
                // Erreur terminale
                throw new Exception("Empty command list, mais pas un Empty Command");
            }

            try
            {
                var firstCommand =
                    commands.OrderedCommands
                            .First();

                if (!firstCommand.Expression.IsT1)
                {
                    throw new NotImplementedException("Unknown command tree type : " + firstCommand.Expression.GetType().Name);
                }

                var firstCliCommand = firstCommand.Expression.AsT1;

                var profile =
                _commandProfiles.Where(c => string.Equals(c.Name, firstCliCommand.Name.Name, StringComparison.CurrentCultureIgnoreCase))
                                .FirstOrDefault();

                CommandParameterValue[] args;

                if (profile == null)
                {
                    // Commande inconnue
                    profile = _commandProfiles.Where(c => string.Equals(c.Name, "UnknownCommand", StringComparison.CurrentCultureIgnoreCase)).First();
                    args = new CommandParameterValue[] { new CommandParameterValue() { Value = firstCliCommand.Name.Name } };
                }
                else
                {
                    args = ConvertArguments(firstCliCommand, profile);
                }

                ValidateArguments(args, profile);

                ICommandAction commandAction = (ICommandAction)_serviceProvider.GetRequiredService(profile.CommandActionType);


                // Ajouter le texte du prompt comme ligne
                var promptLine = scope.NewLine();

                var promptText = _ecs.NewEntity("Prompt").AddComponent<TextComponent>();

                // TODO
                promptText.Text =
                    Input.PromptLines
                         .SelectMany(line => line.LineSegments)
                         .OfType<TextComponent>()
                         .FirstOrDefault()
                         ?.ToText()
                         ?? "";

                promptLine.AddLineSegment(promptText);

                if (commandAction is CommandActionSync syncCommand)
                {
                    _commandHistoryModule.RegisterCommandAsStarted(commandAction, args, scope);
                    syncCommand.Invoke(args, scope);
                    _commandHistoryModule.RegisterCommandAsFinished(commandAction, args, scope);
                }

                else if (commandAction is CommandActionAsync asyncCommand)
                {
                    _commandHistoryModule.RegisterCommandAsStarted(commandAction, args, scope);

                    asyncCommand.CancellationTokenSource = new(); // TODO store in more appropriate place
                    asyncCommand.CurrentTask = Task.Run(async () => await asyncCommand.BeginInvoke(args, scope, asyncCommand.CancellationTokenSource.Token), asyncCommand.CancellationTokenSource.Token);
                    asyncCommand.CurrentTask.ContinueWith(async task => 
                    {
                        if (task.IsCompletedSuccessfully)
                        {
                            await asyncCommand.EndInvoke(args, scope);
                        }
                        else // Could call undo here
                        {
                            await asyncCommand.FailedInvoke(args, scope, task);
                        }

                        // TODO Needs to be synchronous for the undo to be flawless
                        asyncCommand.CancellationTokenSource = null;
                        asyncCommand.CurrentTask = null;
                    });
                }
                else
                {
                    throw new InvalidOperationException("Unknown command type : " + commandAction.GetType().Name);
                }

            }
            catch (ConsoleError e)
            {
                WriteError(scope, e.Message);
            }
        }

        private void WriteError(CliBlock scope, string message)
        {
            var line = scope.NewLine();

            var promptText = _ecs.NewEntity("Error").AddComponent<TextComponent>();
            promptText.Text = message;
            line.AddLineSegment(promptText);
        }

        // TODO 
        private CommandParameterValue[] ConvertArguments(CommandExpressionCli firstCommand, CommandDefinition? profile)
        {
            List<CommandParameterValue> args = new();
            int j = 0;
            for (int i = 0; i < profile.Parameters.Length; i++)
            {
                var param = profile.Parameters[i];

                if (firstCommand.Arguments.Arguments.Count <= j)
                {
                    throw new ConsoleError("Insufficent arguments");
                }

                var arg = firstCommand.Arguments.Arguments[j++];

                CommandArgument ConsumeOneAhead()
                {
                    //return firstCommand.Arguments.Arguments[j++];
                    throw new NotImplementedException("ConsumeOneAhead");
                }

                var value = ConvertArgumentToParameter(param, arg, ConsumeOneAhead);

                args.Add(value);
            }

            return args.ToArray();
        }

        private CommandParameterValue ConvertArgumentToParameter(CommandParameter parameterDefinition, CommandArgument arg, Func<CommandArgument> consumeOneAhead)
        {
            Value value = GetArgumentValue(arg);

            if (value is StringConstant str)
            {
                return new CommandParameterValue()
                {
                    Parameter = parameterDefinition,
                    Value = str.Value
                };
            }

            throw new NotImplementedException("Unknown argument type : " + arg.GetType().Name);
        }

        private static Value GetArgumentValue(CommandArgument arg)
        {
            if (arg is OptionalCommandArgument oa)
            {
                return oa.Value;
            }
            else if (arg is RequiredCommandArgument ra)
            {
                return ra.Value;
            }
            else if (arg is CommandArgumentValue av)
            {
                return av.Value;
            }

            throw new NotImplementedException("Unknown argument type : " + arg.GetType().Name);
        }

        private void ValidateArguments(CommandParameterValue[] args, CommandDefinition? profile)
        {
            // TODO 
        }

    }
}
