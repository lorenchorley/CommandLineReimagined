using CommandLine.Modules;
using Terminal.Execution;

namespace Commands.Implementations
{
    public class ChangeDirectory : CommandActionSync
    {
        private readonly PathModule _pathModule;

        private string? _previousFolder;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "cd",
                Description: "Enter a directory",
                KeyWords: "move navigate directory folder",
                Parameters: new CommandParameter[]
                {
                    // Piped input means `ls | cd` can follow a directory result.
                    new CommandParameter { Name = "TargetPath", Description = "", AcceptsPipedInput = true },
                },
                CommandActionType: typeof(ChangeDirectory)
            );

        public ChangeDirectory(PathModule pathModule)
        {
            _pathModule = pathModule;
        }

        public override RuntimeValue Invoke(CommandInvocation invocation)
        {
            _previousFolder = _pathModule.CurrentPath;

            string target = invocation.ValueOrInput("TargetPath").ToArgumentString();

            if (!_pathModule.Enter(target))
            {
                throw new ConsoleError($"Directory does not exist : {target}");
            }

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
