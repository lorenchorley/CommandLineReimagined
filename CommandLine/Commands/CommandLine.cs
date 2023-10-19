using CommandLineReimagine.Commands.Modules;
using CommandLineReimagine.Commands.Parser;
using CommandLineReimagine.Commands.Parser.SemanticTree;
using CommandLineReimagine.Console;
using CommandLineReimagine.Console.Components;
using EntityComponentSystem;
using Microsoft.Extensions.DependencyInjection;

namespace CommandLineReimagine.Commands
{
    public class CommandLine
    {
        private readonly List<CommandProfile> _commandProfiles;
        private readonly CommandLineInterpreter _interpreter;
        private readonly IServiceProvider _serviceProvider;
        private readonly ECS _ecs;
        private readonly ConsoleLayout _consoleRenderer;
        private readonly CommandHistoryModule _commandHistoryModule;
        private readonly ConsoleOutModule _consoleOutModule;

        public CommandLine(IServiceProvider serviceProvider, ECS ecs, IEnumerable<CommandAction> commandActions, ConsoleLayout consoleRenderer, CommandHistoryModule commandHistoryModule, ConsoleOutModule consoleOutModule)
        {
            _commandProfiles = commandActions.Select(c => c.Profile).ToList();
            _interpreter = new CommandLineInterpreter();
            _serviceProvider = serviceProvider;
            _ecs = ecs;
            _consoleRenderer = consoleRenderer;
            _commandHistoryModule = commandHistoryModule;
            _consoleOutModule = consoleOutModule;
        }

        public void RegisterCommand(CommandProfile commandProfile)
        {
            _commandProfiles.Add(commandProfile);
        }

        public void ExecuteCommand(string command)
        {
            ParserResult result = _interpreter.Parse(command);

            var scope = _consoleOutModule.StartScope(command);
            try
            {
                if (result.HasErrors)
                {
                    WriteError(scope, result.Errors.Join(", "));
                    return;
                }

                if (result.Tree is EmptyCommand)
                {
                    // Fait rien
                    return;
                }

                if (result.Tree is not PipedCommandList commands)
                {
                    // Erreur terminale
                    throw new Exception("Unknown command tree type : " + result.Tree?.GetType().Name);
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

                    var profile =
                        _commandProfiles.Where(c => string.Equals(c.Name, firstCommand.Id.Name, StringComparison.CurrentCultureIgnoreCase))
                                        .FirstOrDefault();

                    CommandParameterValue[] args;

                    if (profile == null)
                    {
                        // Commande inconnue
                        profile = _commandProfiles.Where(c => string.Equals(c.Name, "UnknownCommand", StringComparison.CurrentCultureIgnoreCase)).First();
                        args = new CommandParameterValue[] { new CommandParameterValue() { Value = firstCommand.Id.Name } };
                    }
                    else
                    {
                        args = ConvertArguments(firstCommand, profile);
                    }

                    ValidateArguments(args, profile);

                    var commandAction = (CommandAction)_serviceProvider.GetRequiredService(profile.CommandActionType);


                    // Ajouter le texte du prompt comme ligne
                    var promptLine = scope.NewLine();

                    var promptText = _ecs.NewEntity("Prompt").AddComponent<TextBlock>();
                    promptText.Text = $"{_consoleRenderer.Input.ActiveLine.ToText()}";
                    promptLine.AddLineSegment(promptText);

                    commandAction.Execute(args.ToArray(), scope);

                    _commandHistoryModule.RegisterCommandAsExecuted(commandAction, args, scope);
                }
                catch (ConsoleError e)
                {
                    WriteError(scope, e.Message);
                }
            }
            finally
            {
                scope.Finalise();
            }
        }

        private void WriteError(ConsoleOutScope scope, string message)
        {
            var line = scope.NewLine();

            var promptText = _ecs.NewEntity("Error").AddComponent<TextBlock>();
            promptText.Text = message;
            line.AddLineSegment(promptText);
        }

        // TODO 
        private CommandParameterValue[] ConvertArguments(CommandExpression firstCommand, CommandProfile? profile)
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
            if (arg is StringConstant str)
            {
                return new CommandParameterValue()
                {
                    Parameter = parameterDefinition,
                    Value = str.Value[1..^1] // Enleve les doubles quotes
                };
            }
            else if (arg is Flag flag)
            {
                // TODO 
                throw new NotImplementedException("flag");
            }

            throw new NotImplementedException("Unknown argument type : " + arg.GetType().Name);
        }

        private void ValidateArguments(CommandParameterValue[] args, CommandProfile? profile)
        {
            // TODO 
        }
    }
}
