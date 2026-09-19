using Terminal.Execution;

namespace Commands
{
    /// <summary>
    /// A command that runs over time and can be cancelled.
    /// </summary>
    public abstract class CommandActionAsync : ICommandAction
    {
        // TODO Place more appropriately
        public CancellationTokenSource? CancellationTokenSource { get; set; }
        public Task? CurrentTask { get; set; }
        public bool AlreadyCancelled { get; set; } = false;

        public abstract CommandDefinition Profile { get; }

        public abstract Task<RuntimeValue> BeginInvoke(CommandInvocation invocation);

        public abstract Task EndInvoke(CommandInvocation invocation);

        public abstract Task FailedInvoke(CommandInvocation invocation, Task task);

        public abstract Task BeginInvokeUndo(CommandInvocation invocation);
    }
}
