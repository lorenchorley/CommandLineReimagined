using System.Collections.Generic;
using Commands;
using Console;

namespace CommandLine.Modules
{
    public class CommandHistoryModule
    {
        private readonly ConsoleLayout _consoleRenderer;

        public Stack<(CommandAction Action, CommandParameterValue[] Args, ConsoleOutBlock Scope)> ExecutedCommands { get; set; } = new();

        public CommandHistoryModule(ConsoleLayout consoleRenderer)
        {
            _consoleRenderer = consoleRenderer;
        }

        public void RegisterCommandAsExecuted(CommandAction action, CommandParameterValue[] args, ConsoleOutBlock scope)
        {
            ExecutedCommands.Push((action, args, scope));
        }

        public void UndoLastCommand()
        {
            if (ExecutedCommands.Count == 0)
            {
                return;
            }

            var lastCommand = ExecutedCommands.Pop();
            lastCommand.Action.Undo(lastCommand.Args, lastCommand.Scope);

            foreach (var line in lastCommand.Scope.Lines)
            {
                _consoleRenderer.Output.Lines.Remove(line);
                line.Entity.Destroy();
            }

            lastCommand.Scope.Lines.Clear();
        }
    }
}
