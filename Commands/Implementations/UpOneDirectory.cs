using CommandLine.Modules;
using Terminal.Execution;

namespace Commands.Implementations
{
    public class UpOneDirectory : CommandActionSync
    {
        private readonly PathModule _pathModule;

        private string? _previousFolder;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "up",
                Description: "Move up one directory",
                KeyWords: "move parent back navigate",
                Parameters: new CommandParameter[]
                {
                },
                CommandActionType: typeof(UpOneDirectory)
            );

        public UpOneDirectory(PathModule pathModule)
        {
            _pathModule = pathModule;
        }

        public override RuntimeValue Invoke(CommandInvocation invocation)
        {
            _previousFolder = _pathModule.CurrentPath;
            _pathModule.Up();

            return new PathValue(_pathModule.CurrentPath, PathKind.Directory);
        }

        public override void InvokeUndo(CommandInvocation invocation)
        {
            if (_previousFolder is not null)
            {
                _pathModule.MoveTo(_previousFolder);
            }
        }
    }
}
