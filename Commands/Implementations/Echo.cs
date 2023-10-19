using System.IO;
using System.Xml.Linq;
using CommandLineReimagine.Commands.Modules;
using CommandLineReimagine.Console;
using CommandLineReimagine.Console.Components;

namespace CommandLineReimagine.Commands.Implementations
{
    public class Echo : CommandAction
    {
        public override CommandProfile Profile { get; } =
            new CommandProfile(
                Name: "echo",
                Description: "",
                Parameters: new CommandParameter[]
                {
                    new CommandParameter() { Name = "text", Description = "" }
                },
                CommandActionType: typeof(Echo)
            );

        public override void Execute(CommandParameterValue[] args, ConsoleOutScope scope)
        {
            Line line = scope.NewLine();

            line.AddTextBlock("echo", args[0].Value);
        }

        public override void Undo(CommandParameterValue[] args, ConsoleOutScope scope)
        {
        }
    }
}
