using System.IO;
using System.Xml.Linq;
using CommandLine.Modules;
using Console.Components;

namespace Commands.Implementations
{
    public class Echo : CommandAction
    {
        public override Command Profile { get; } =
            new Command(
                Name: "echo",
                Description: "",
                Parameters: new CommandParameter[]
                {
                    new CommandParameter() { Name = "text", Description = "" }
                },
                CommandActionType: typeof(Echo)
            );

        public override void Execute(CommandParameterValue[] args, ConsoleOutBlock scope)
        {
            LineComponent line = scope.NewLine();

            line.AddTextBlock("echo", args[0].Value);
        }

        public override void Undo(CommandParameterValue[] args, ConsoleOutBlock scope)
        {
        }
    }
}
