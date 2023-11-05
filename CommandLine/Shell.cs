using CommandLine.Modules;
using Commands;
using Commands.Parser;
using Commands.Parser.SemanticTree;
using Console;
using Console.Components;
using EntityComponentSystem;
using Microsoft.Extensions.DependencyInjection;
using OneOf;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Terminal.Naming;
using Terminal.Search;

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

    //public class CommandFailedParsing
    //{
    //    public int Line { get; set; }
    //    public int Column { get; set; }
    //    public ParsingErrorType ErrorType { get; set; }
    //    public List<string> Errors { get; set; }

    //    private static readonly Regex _matchSyntaxError = new Regex(@"(?<errorType>SyntaxError|LexicalError) à la ligne (?<line>[\d]) et à la colonne (?<column>[\d])");

    //    public CommandFailedParsing(List<string> errors)
    //    {
    //        var first = errors[0];
    //        var matches = _matchSyntaxError.Match(first);
    //        if (matches.Success)
    //        {
    //            ErrorType = matches.Groups["errorType"].Value switch
    //            {
    //                "SyntaxError" => ParsingErrorType.SyntaxError,
    //                "LexicalError" => ParsingErrorType.LexicalError,
    //                _ => throw new NotImplementedException("Unknown error type : " + matches.Groups["errorType"].Value)
    //            };
    //            Line = int.Parse(matches.Groups["line"].Value);
    //            Column = int.Parse(matches.Groups["column"].Value);
    //        }
    //        else
    //        {
    //            throw new NotImplementedException("Unknown error type : " + first);
    //        }

    //        Errors = errors.Skip(1).ToList();
    //    }
    //}
    
    public class CommandFailedTypeChecking
    {

    }

    public class Shell
    {
        private readonly List<CommandDefinition> _commandProfiles;
        private readonly CommandLineInterpreter _interpreter;
        private readonly IServiceProvider _serviceProvider;
        private readonly ECS _ecs;
        private readonly ConsoleLayout _consoleRenderer;
        private readonly CommandHistoryModule _commandHistoryModule;
        private readonly ConsoleOutModule _consoleOutModule;
        private readonly NameResolver _nameResolver;
        private readonly Prompt _prompt;
        private readonly CommandSearch _commandSearch;

        public Shell(IServiceProvider serviceProvider,
                     ECS ecs,
                     IEnumerable<CommandAction> commandActions,
                     ConsoleLayout consoleRenderer,
                     CommandHistoryModule commandHistoryModule,
                     ConsoleOutModule consoleOutModule,
                     NameResolver nameResolver,
                     Prompt prompt,
                     CommandSearch commandSearch)
        {
            _commandProfiles = commandActions.Select(c => c.Profile).ToList();
            _interpreter = new CommandLineInterpreter();
            _serviceProvider = serviceProvider;
            _ecs = ecs;
            _consoleRenderer = consoleRenderer;
            _commandHistoryModule = commandHistoryModule;
            _consoleOutModule = consoleOutModule;
            _nameResolver = nameResolver;
            _prompt = prompt;
            _commandSearch = commandSearch;

            _commandSearch.AsynchronouslyLoadIndexes();
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
                consoleBlock.Finalise();
            }

            return true;
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

                var commandAction = (CommandAction)_serviceProvider.GetRequiredService(profile.CommandActionType);


                // Ajouter le texte du prompt comme ligne
                var promptLine = scope.NewLine();

                var promptText = _ecs.NewEntity("Prompt").AddComponent<TextComponent>();

                // TODO
                promptText.Text = 
                    _consoleRenderer.Input
                                    .PromptLines
                                    .SelectMany(line => line.GetOrderedLineSegments())
                                    .OfType<TextComponent>()
                                    .FirstOrDefault()
                                    ?.ToText()
                                    ?? "";

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
