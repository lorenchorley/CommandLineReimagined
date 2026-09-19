using Terminal.Execution;

namespace Commands.Implementations
{
    public class UnknownCommand : CommandActionSync
    {
        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "UnknownCommand",
                Description: "Reports a command name that could not be resolved",
                KeyWords: "",
                Parameters: new CommandParameter[]
                {
                    new CommandParameter { Name = "name", Description = "" },
                },
                CommandActionType: typeof(UnknownCommand)
            );

        public override RuntimeValue Invoke(CommandInvocation invocation)
        {
            string name = invocation.Arguments.Count > 0
                ? invocation.Arguments[0].Text
                : string.Empty;

            throw new ConsoleError($"Unknown command : {name}");
        }

        public override void InvokeUndo(CommandInvocation invocation)
        {
        }
    }
}
