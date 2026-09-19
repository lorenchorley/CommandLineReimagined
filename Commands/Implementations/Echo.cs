using Terminal.Execution;

namespace Commands.Implementations
{
    public class Echo : CommandActionSync
    {
        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "echo",
                Description: "Writes its argument, or whatever was piped into it",
                KeyWords: "print write output",
                Parameters: new CommandParameter[]
                {
                    // Accepting piped input is what makes `ls | echo` work.
                    new CommandParameter { Name = "text", Description = "", AcceptsPipedInput = true },
                },
                CommandActionType: typeof(Echo)
            );

        public override RuntimeValue Invoke(CommandInvocation invocation)
        {
            var value = invocation.ValueOrInput("text");

            return value;
        }

        public override void InvokeUndo(CommandInvocation invocation)
        {
        }
    }
}
