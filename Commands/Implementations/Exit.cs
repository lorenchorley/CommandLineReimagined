using Terminal.Execution;

namespace Commands.Implementations
{
    public class Exit : CommandActionSync
    {
        private readonly IApplicationLifetime _lifetime;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "exit",
                Description: "Close the application",
                KeyWords: "quit close stop",
                Parameters: new CommandParameter[]
                {
                },
                CommandActionType: typeof(Exit)
            );

        public Exit(IApplicationLifetime lifetime)
        {
            _lifetime = lifetime;
        }

        public override RuntimeValue Invoke(CommandInvocation invocation)
        {
            // Previously a comment saying this needed an injected dependency. It has one
            // now, so the host decides what shutting down means and the command does not
            // reach for WPF.
            _lifetime.Shutdown();

            return RuntimeValue.Empty;
        }

        public override void InvokeUndo(CommandInvocation invocation)
        {
        }
    }
}
