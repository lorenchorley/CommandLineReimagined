﻿using System.IO;
using System.Xml.Linq;
using CommandLine.Modules;
using UIComponents.Components;

namespace Commands.Implementations
{
    public class CopyFile : CommandActionSync
    {
        private readonly PathModule _pathModule;

        private string _originalFilename;
        private string _targetPath;
        private string _targetFilename;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "cp",
                Description: "",
                KeyWords: "",
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

        public override void Invoke(CommandParameterValue[] args, CliBlock scope)
        {
            TextComponent segment;
            var line = scope.NewLine();

            _originalFilename = args[0].Value;

            if (!File.Exists(_originalFilename))
            {
                line.LinkNewTextBlock("cp error", $"File does not exist : {_pathModule.CurrentFolder}");
                return;
            }

            _targetPath = args[1].Value;

            if (!Directory.Exists(_targetPath))
            {
                line.LinkNewTextBlock("cp error", $"Target directory does not exist : {_pathModule.CurrentFolder}");
                return;
            }

            _targetFilename = Path.Combine(_targetPath, Path.GetFileName(_originalFilename));

            if (File.Exists(_targetFilename))
            {
                line.LinkNewTextBlock("cp error", $"Target file already exists : {_pathModule.CurrentFolder}");
                return;
            }

            File.Copy(_originalFilename, _targetFilename);

            line.LinkNewTextBlock("cp", $"Moved file to : {_pathModule.CurrentFolder}");
        }

        public override void InvokeUndo(CommandParameterValue[] args, CliBlock scope)
        {
            File.Delete(_targetFilename);
        }
    }
}
