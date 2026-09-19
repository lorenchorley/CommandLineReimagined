using CommandLine.Modules;
using Terminal.Execution;

namespace Commands.Implementations
{
    public class CopyFile : CommandActionSync
    {
        private readonly PathModule _pathModule;

        private string? _targetFilename;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "cp",
                Description: "Copy a file into a directory",
                KeyWords: "copy duplicate file",
                Parameters: new CommandParameter[]
                {
                    new CommandParameter { Name = "sourcePathAndFile", Description = "" },
                    new CommandParameter { Name = "targetPath", Description = "" },
                },
                CommandActionType: typeof(CopyFile)
            );

        public CopyFile(PathModule pathModule)
        {
            _pathModule = pathModule;
        }

        public override RuntimeValue Invoke(CommandInvocation invocation)
        {
            string source = Resolve(invocation.Text("sourcePathAndFile"));

            // Reports the offending path rather than the current folder, which is what
            // the old messages printed regardless of what actually failed.
            if (!File.Exists(source))
            {
                throw new ConsoleError($"File does not exist : {source}");
            }

            string targetPath = Resolve(invocation.Text("targetPath"));

            if (!Directory.Exists(targetPath))
            {
                throw new ConsoleError($"Target directory does not exist : {targetPath}");
            }

            _targetFilename = Path.Combine(targetPath, Path.GetFileName(source));

            if (File.Exists(_targetFilename))
            {
                throw new ConsoleError($"Target file already exists : {_targetFilename}");
            }

            File.Copy(source, _targetFilename);

            return new PathValue(_targetFilename, PathKind.File);
        }

        public override void InvokeUndo(CommandInvocation invocation)
        {
            if (_targetFilename is not null && File.Exists(_targetFilename))
            {
                File.Delete(_targetFilename);
            }
        }

        private string Resolve(string path) =>
            Path.IsPathRooted(path) ? path : Path.Combine(_pathModule.CurrentPath, path);
    }
}
