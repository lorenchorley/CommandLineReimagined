using System.IO;
using System.Windows;
using System.Xml.Linq;
using CommandLine.Modules;

namespace CommandLineReimagined.Commands.Implementations
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
            //Application.Current.Shutdown();
            // Il faut demander ça via une dépendance injectée
        }

        public override void Undo(CommandParameterValue[] args, ConsoleOutScope scope)
        {
        }
    }
}
