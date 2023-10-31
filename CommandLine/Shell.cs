using CommandLine.Modules;
using Commands;
using Commands.Parser;
using Commands.Parser.SemanticTree;
using Console;
using Console.Components;
using EntityComponentSystem;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Naming;

namespace Terminal
{
    public class Shell
    {
        private readonly List<Command> _commandProfiles;
        private readonly CommandLineInterpreter _interpreter;
        private readonly IServiceProvider _serviceProvider;
        private readonly ECS _ecs;
        private readonly ConsoleLayout _consoleRenderer;
        private readonly CommandHistoryModule _commandHistoryModule;
        private readonly ConsoleOutModule _consoleOutModule;
        private readonly NameResolver _nameResolver;

        public Shell(IServiceProvider serviceProvider,
                     ECS ecs,
                     IEnumerable<CommandAction> commandActions,
                     ConsoleLayout consoleRenderer,
                     CommandHistoryModule commandHistoryModule,
                     ConsoleOutModule consoleOutModule,
                     NameResolver nameResolver)
        {
            _commandProfiles = commandActions.Select(c => c.Profile).ToList();
            _interpreter = new CommandLineInterpreter();
            _serviceProvider = serviceProvider;
            _ecs = ecs;
            _consoleRenderer = consoleRenderer;
            _commandHistoryModule = commandHistoryModule;
            _consoleOutModule = consoleOutModule;
            _nameResolver = nameResolver;
        }

        public void RegisterCommand(Command commandProfile)
        {
            _commandProfiles.Add(commandProfile);
        }

        public bool IsCommandExecutable(string command)
        {
            return true;
        }

        public void ExecuteCommand(string command)
        {
            ParserResult result = _interpreter.Parse(command);

            var scope = _consoleOutModule.StartScope(command);
            try
            {
                result.Switch(
                    result => ExecuteNominal(result, scope),
                    error => WriteError(scope, result.AsT1.Join(", "))
                    );
            }
            finally
            {
                scope.Finalise();
            }
        }

        private void ExecuteNominal(RootNode result, ConsoleOutBlock scope)
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


                if (!firstCommand.Expression.IsT0)
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

                var commandAction = (CommandAction)_serviceProvider.GetRequiredService(profile.CommandActionType);


                // Ajouter le texte du prompt comme ligne
                var promptLine = scope.NewLine();

                var promptText = _ecs.NewEntity("Prompt").AddComponent<TextComponent>();
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

        private void WriteError(ConsoleOutBlock scope, string message)
        {
            var line = scope.NewLine();

            var promptText = _ecs.NewEntity("Error").AddComponent<TextComponent>();
            promptText.Text = message;
            line.AddLineSegment(promptText);
        }

        // TODO 
        private CommandParameterValue[] ConvertArguments(CommandExpressionCli firstCommand, Command? profile)
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
                    Value = str.Value[1..^1] // Enleve les doubles quotes
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
            throw new NotImplementedException("Unknown argument type : " + arg.GetType().Name);
        }

        private void ValidateArguments(CommandParameterValue[] args, Command? profile)
        {
            // TODO 
        }
    }
}
