using CommandLineReimagine.Commands.Modules;

namespace CommandLineReimagine.Commands.Implementations
{
    public class UnknownCommand : CommandAction
    {

        public override CommandProfile Profile { get; } =
            new CommandProfile(
                Name: "UnknownCommand",
                Description: "",
                Parameters: new CommandParameter[]
                {
                },
                CommandActionType: typeof(UnknownCommand)
            );

        public override void Execute(CommandParameterValue[] args, ConsoleOutScope scope)
        {
            var line = scope.NewLine();

            line.AddTextBlock("Unknown command", $"Unknown command : {args[0].Value}");
        }

        public override void Undo(CommandParameterValue[] args, ConsoleOutScope scope)
        {
        }
    }
}
