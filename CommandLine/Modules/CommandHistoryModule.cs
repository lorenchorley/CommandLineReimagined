using System.Collections.Generic;
using Commands;
using UIComponents;

namespace CommandLine.Modules
{
    public class CommandHistoryModule
    {
        public Stack<(ICommandAction Action, CommandParameterValue[] Args, CliBlock CliBlock)> ExecutedCommands { get; set; } = new();

        public void RegisterCommandAsStarted(ICommandAction action, CommandParameterValue[] args, CliBlock scope)
        {
            ExecutedCommands.Push((action, args, scope));
        }
        
        public void RegisterCommandAsFinished(ICommandAction action, CommandParameterValue[] args, CliBlock scope)
        {
        }

        public void UndoLastCommand()
        {
            if (ExecutedCommands.Count == 0)
            {
                return;
            }

            var (action, args, cliBlock) = ExecutedCommands.Pop();

            if (action is CommandActionSync syncCommand)
            {
                syncCommand.InvokeUndo(args, cliBlock);

                cliBlock.Clear();
            }
            else if (action is CommandActionAsync asyncCommand)
            {
                // Allows for the undo to asynchronously stop and still show output, while a second undo will clear the output
                if (asyncCommand.AlreadyCancelled) 
                {
                    cliBlock.Clear();
                    return;
                }

                asyncCommand.AlreadyCancelled = true;
                ExecutedCommands.Push((asyncCommand, args, cliBlock));

                // TODO Needs to be synchronous for the undo to be flawless
                if (asyncCommand.CurrentTask == null)
                {
                    Task.Run(async () => await asyncCommand.BeginInvokeUndo(args, cliBlock));
                }
                else
                {
                    // Theres a chance here that the task will already be completed and so the callback wont get run
                    asyncCommand.CancellationTokenSource?.Cancel();
                    asyncCommand.CurrentTask?.ContinueWith(t => asyncCommand.BeginInvokeUndo(args, cliBlock)); 

                }

            }
        }
    }
}
