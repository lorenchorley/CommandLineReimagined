using CommandLine.Modules;

namespace Commands.Implementations
{
    public class UnknownCommand : CommandActionSync
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

        public override void Invoke(CommandParameterValue[] args, CliBlock scope)
        {
            var line = scope.NewLine();

            line.LinkNewTextBlock("Unknown command", $"Unknown command : {args[0].Value}");
        }

        public override void InvokeUndo(CommandParameterValue[] args, CliBlock scope)
        {
        }
    }
}
