using CommandLine.Modules;
using Terminal.Execution;

namespace Commands.Implementations
{
    public class MakeDirectory : CommandActionSync
    {
        private readonly PathModule _pathModule;

        private string? _targetFolder;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "mkdir",
                Description: "Create a directory",
                KeyWords: "create make directory folder",
                Parameters: new CommandParameter[]
                {
                    new CommandParameter { Name = "FolderName", Description = "" },
                },
                CommandActionType: typeof(MakeDirectory)
            );

        public MakeDirectory(PathModule pathModule)
        {
            _pathModule = pathModule;
        }

        public override RuntimeValue Invoke(CommandInvocation invocation)
        {
            _targetFolder = Path.Combine(_pathModule.CurrentPath, invocation.Text("FolderName"));

            if (Directory.Exists(_targetFolder))
            {
                throw new ConsoleError($"Target directory already exists : {_targetFolder}");
            }

            Directory.CreateDirectory(_targetFolder);

            return new PathValue(_targetFolder, PathKind.Directory);
        }

        public override void InvokeUndo(CommandInvocation invocation)
        {
            if (_targetFolder is not null && Directory.Exists(_targetFolder))
            {
                Directory.Delete(_targetFolder);
            }
        }
    }
}
