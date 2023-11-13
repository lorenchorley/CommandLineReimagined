using System.IO;
using System.Xml.Linq;
using CommandLine.Modules;
using UIComponents.Components;

namespace Commands.Implementations
{
    public class Echo : CommandActionSync
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

        public override void Invoke(CommandParameterValue[] args, CliBlock scope)
        {
            LineComponent line = scope.NewLine();

            line.LinkNewTextBlock("echo", args[0].Value);
        }

        public override void InvokeUndo(CommandParameterValue[] args, CliBlock scope)
        {
        }
    }
}
