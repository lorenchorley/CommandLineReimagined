using System.IO;
using System.Xml.Linq;
using CommandLine.Modules;
using Console.Components;

namespace Commands.Implementations
{
    public class Echo : CommandAction
    {
        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "echo",
                Description: "",
                KeyWords: "",
                Parameters: new CommandParameter[]
                {
                    new CommandParameter() { Name = "text", Description = "" }
                },
                CommandActionType: typeof(Echo)
            );

        public override void Execute(CommandParameterValue[] args, ConsoleOutBlock scope)
        {
            LineComponent line = scope.NewLine();

            line.LinkNewTextBlock("echo", args[0].Value);
        }

        public override void Undo(CommandParameterValue[] args, ConsoleOutBlock scope)
        {
        }
    }
}
