﻿using System.IO;
using System.Xml.Linq;
using CommandLine.Modules;

namespace Commands.Implementations
{
    public class MakeDirectory : CommandActionSync
    {
        private readonly PathModule _pathModule;

        private string _targetFolder;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "mkdir",
                Description: "",
                KeyWords: "",
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

        public override void Invoke(CommandParameterValue[] args, CliBlock scope)
        {
            var line = scope.NewLine();

            _targetFolder = Path.Combine(_pathModule.CurrentPath, args[0].Value);

            if (Directory.Exists(_targetFolder))
            {
                line.LinkNewTextBlock("cp error", $"Target directory already exists : {_targetFolder}");

                return;
            }

            Directory.CreateDirectory(_targetFolder);

            line.LinkNewTextBlock("mkdir", $"Created directory : {_targetFolder}");
        }

        public override void InvokeUndo(CommandParameterValue[] args, CliBlock scope)
        {
            Directory.Delete(_targetFolder);
        }
    }
}
