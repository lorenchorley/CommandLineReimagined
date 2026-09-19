using Terminal.Execution;

namespace Commands
{
    /// <summary>
    /// A command that completes within one call.
    /// </summary>
    /// <remarks>
    /// Invoke returns a <see cref="RuntimeValue"/> rather than void: a command that
    /// produces nothing for the next stage of a pipe returns
    /// <see cref="RuntimeValue.Empty"/>, and anything else becomes the pipe's value.
    /// </remarks>
    public abstract class CommandActionSync : ICommandAction
    {
        public abstract CommandDefinition Profile { get; }

        public abstract RuntimeValue Invoke(CommandInvocation invocation);

        public abstract void InvokeUndo(CommandInvocation invocation);
    }
}
