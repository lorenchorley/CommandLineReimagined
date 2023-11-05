using System.IO;
using System.Windows;
using System.Xml.Linq;
using CommandLine.Modules;

namespace Commands.Implementations
{
    public class Exit : CommandAction
    {
        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "exit",
                Description: "",
                KeyWords: "",
                Parameters: new CommandParameter[]
                {
                },
                CommandActionType: typeof(Exit)
            );

        public override void Execute(CommandParameterValue[] args, ConsoleOutBlock scope)
        {
            //Application.Current.Shutdown();
            // Il faut demander ça via une dépendance injectée
        }

        public override void Undo(CommandParameterValue[] args, ConsoleOutBlock scope)
        {
        }
    }
}
