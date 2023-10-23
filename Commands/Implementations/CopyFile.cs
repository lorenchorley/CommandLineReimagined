using System.IO;
using System.Xml.Linq;
using CommandLine.Modules;
using Console.Components;

namespace Commands.Implementations
{
    public class CopyFile : CommandAction
    {
        private readonly PathModule _pathModule;

        private string _originalFilename;
        private string _targetPath;
        private string _targetFilename;

        public override CommandProfile Profile { get; } =
            new CommandProfile(
                Name: "cp",
                Description: "",
                Parameters: new CommandParameter[]
                {
                    new CommandParameter() { Name = "sourcePathAndFile", Description = "" },
                    new CommandParameter() { Name = "targetPath", Description = "" }
                },
                CommandActionType: typeof(CopyFile)
            );

        public CopyFile(PathModule pathModule)
        {
            _pathModule = pathModule;
        }

        public override void Execute(CommandParameterValue[] args, ConsoleOutScope scope)
        {
            TextBlock segment;
            var line = scope.NewLine();

            _originalFilename = args[0].Value;

            if (!File.Exists(_originalFilename))
            {
                line.AddTextBlock("cp error", $"File does not exist : {_pathModule.CurrentFolder}");
                return;
            }

            _targetPath = args[1].Value;

            if (!Directory.Exists(_targetPath))
            {
                line.AddTextBlock("cp error", $"Target directory does not exist : {_pathModule.CurrentFolder}");
                return;
            }

            _targetFilename = Path.Combine(_targetPath, Path.GetFileName(_originalFilename));

            if (File.Exists(_targetFilename))
            {
                line.AddTextBlock("cp error", $"Target file already exists : {_pathModule.CurrentFolder}");
                return;
            }

            File.Copy(_originalFilename, _targetFilename);

            line.AddTextBlock("cp", $"Moved file to : {_pathModule.CurrentFolder}");
        }

        public override void Undo(CommandParameterValue[] args, ConsoleOutScope scope)
        {
            File.Delete(_targetFilename);
        }
    }
}
