using System.IO;
using System.Windows;
using System.Xml.Linq;
using CommandLineReimagine.Commands.Modules;
using CommandLineReimagine.Console;
using CommandLineReimagine.Console.Components;

namespace CommandLineReimagine.Commands.Implementations
{
    public class Exit : CommandAction
    {
        public override CommandProfile Profile { get; } =
            new CommandProfile(
                Name: "exit",
                Description: "",
                Parameters: new CommandParameter[]
                {
                },
                CommandActionType: typeof(Exit)
            );

        public override void Execute(CommandParameterValue[] args, ConsoleOutScope scope)
        {
            Application.Current.Shutdown();
        }

        public override void Undo(CommandParameterValue[] args, ConsoleOutScope scope)
        {
        }
    }
}
