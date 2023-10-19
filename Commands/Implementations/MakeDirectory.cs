using System.IO;
using System.Xml.Linq;
using CommandLineReimagine.Commands.Modules;
using CommandLineReimagine.Console;
using CommandLineReimagine.Console.Components;

namespace CommandLineReimagine.Commands.Implementations
{
    public class MakeDirectory : CommandAction
    {
        private readonly PathModule _pathModule;

        private string _targetFolder;

        public override CommandProfile Profile { get; } =
            new CommandProfile(
                Name: "mkdir",
                Description: "",
                Parameters: new CommandParameter[]
                {
                    new CommandParameter() { Name = "FolderName", Description = "" }
                },
                CommandActionType: typeof(MakeDirectory)
            );

        public MakeDirectory(PathModule pathModule)
        {
            _pathModule = pathModule;
        }

        public override void Execute(CommandParameterValue[] args, ConsoleOutScope scope)
        {
            var line = scope.NewLine();

            _targetFolder = Path.Combine(_pathModule.CurrentPath, args[0].Value);

            if (Directory.Exists(_targetFolder))
            {
                line.AddTextBlock("cp error", $"Target directory already exists : {_targetFolder}");

                return;
            }

            Directory.CreateDirectory(_targetFolder);

            line.AddTextBlock("mkdir", $"Created directory : {_targetFolder}");
        }

        public override void Undo(CommandParameterValue[] args, ConsoleOutScope scope)
        {
            Directory.Delete(_targetFolder);
        }
    }
}
