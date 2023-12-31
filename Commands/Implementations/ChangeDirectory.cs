﻿using CommandLine.Modules;
using UIComponents.Components;

namespace Commands.Implementations
{
    public class ChangeDirectory : CommandActionSync
    {
        private readonly PathModule _pathModule;

        private string _previousFolder;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "cd",
                Description: "",
                KeyWords: "",
                Parameters: new CommandParameter[]
                {
                    new CommandParameter() { Name = "TargetPath", Description = "" }
                },
                CommandActionType: typeof(ChangeDirectory)
            );

        public ChangeDirectory(PathModule pathModule)
        {
            _pathModule = pathModule;
        }

        public override void Invoke(CommandParameterValue[] args, CliBlock scope)
        {
            TextComponent segment;
            var line = scope.NewLine();

            _previousFolder = _pathModule.CurrentPath;

            string target = args[0].Value;

            if (!_pathModule.Enter(target))
            {
                line.LinkNewTextBlock("cd error", $"Directory does not exist : {target}");
                return;
            }

            scope.AbondonLine(line);
        }

        public override void InvokeUndo(CommandParameterValue[] args, CliBlock scope)
        {
            _pathModule.MoveTo(_previousFolder);
        }
    }
}
