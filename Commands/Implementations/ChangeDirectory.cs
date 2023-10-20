﻿using CommandLine.Modules;
using CommandLineReimagined.Console.Components;

namespace CommandLineReimagined.Commands.Implementations
{
    public class ChangeDirectory : CommandAction
    {
        private readonly PathModule _pathModule;

        private string _previousFolder;

        public override CommandProfile Profile { get; } =
            new CommandProfile(
                Name: "cd",
                Description: "",
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

        public override void Execute(CommandParameterValue[] args, ConsoleOutScope scope)
        {
            TextBlock segment;
            var line = scope.NewLine();

            _previousFolder = _pathModule.CurrentPath;

            string target = args[0].Value;

            if (!_pathModule.Enter(target))
            {
                line.AddTextBlock("cd error", $"Directory does not exist : {target}");
                return;
            }

            line.AddTextBlock("cd", $"Moved to : {target}");
        }

        public override void Undo(CommandParameterValue[] args, ConsoleOutScope scope)
        {
            _pathModule.MoveTo(_previousFolder);
        }
    }
}
