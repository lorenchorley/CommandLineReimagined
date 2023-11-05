using CommandLine.Modules;

namespace Commands.Implementations
{
    public class UnknownCommand : CommandAction
    {

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "UnknownCommand",
                Description: "",
                KeyWords: "",
                Parameters: new CommandParameter[]
                {
                },
                CommandActionType: typeof(UnknownCommand)
            );

        public override void Execute(CommandParameterValue[] args, ConsoleOutBlock scope)
        {
            var line = scope.NewLine();

            line.LinkNewTextBlock("Unknown command", $"Unknown command : {args[0].Value}");
        }

        public override void Undo(CommandParameterValue[] args, ConsoleOutBlock scope)
        {
        }
    }
}
