using CommandLine.Modules;

namespace Commands
{
    public abstract class CommandActionAsync : ICommandAction
    {
        // TODO Place more appropriately
        public CancellationTokenSource? CancellationTokenSource { get; set; }
        public Task? CurrentTask { get; set; }
        public bool AlreadyCancelled { get; set; } = false;

        public abstract CommandDefinition Profile { get; }

        public abstract Task BeginInvoke(CommandParameterValue[] args, CliBlock scope, CancellationToken cancellationToken);
        public abstract Task EndInvoke(CommandParameterValue[] args, CliBlock scope);
        public abstract Task FailedInvoke(CommandParameterValue[] args, CliBlock scope, Task task);

        public abstract Task BeginInvokeUndo(CommandParameterValue[] args, CliBlock scope);
    }
}
