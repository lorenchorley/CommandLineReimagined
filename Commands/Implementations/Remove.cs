using CommandLine.Modules;
using Terminal.Execution;

namespace Commands.Implementations
{
    /// <summary>
    /// Deletes a file or an empty directory. Undo puts it back.
    /// </summary>
    /// <remarks>
    /// Refuses a directory with anything in it: undoing that would mean snapshotting a
    /// whole tree, and a shell that deletes trees on one word is not one to test on a
    /// phone. Emptying the directory first is the intended workflow.
    /// </remarks>
    public class Remove : CommandActionSync
    {
        private readonly PathModule _pathModule;

        private string? _path;
        private PathKind _kind;
        private string? _previousContents;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "rm",
                Description: "Delete a file or an empty directory",
                KeyWords: "remove delete erase file directory",
                Parameters: new CommandParameter[]
                {
                    new CommandParameter { Name = "path", Description = "What to delete", AcceptsPipedInput = true },
                },
                CommandActionType: typeof(Remove)
            );

        public Remove(PathModule pathModule)
        {
            _pathModule = pathModule;
        }

        public override RuntimeValue Invoke(CommandInvocation invocation)
        {
            _path = _pathModule.Resolve(invocation.ValueOrInput("path").ToArgumentString());

            if (File.Exists(_path))
            {
                _kind = PathKind.File;
                _previousContents = File.ReadAllText(_path);
                File.Delete(_path);
            }
            else if (Directory.Exists(_path))
            {
                if (Directory.EnumerateFileSystemEntries(_path).Any())
                {
                    throw new ConsoleError($"Directory is not empty : {_path}");
                }

                if (string.Equals(
                        Path.TrimEndingDirectorySeparator(_path),
                        Path.TrimEndingDirectorySeparator(_pathModule.CurrentPath),
                        StringComparison.Ordinal))
                {
                    throw new ConsoleError("Cannot delete the current directory.");
                }

                _kind = PathKind.Directory;
                Directory.Delete(_path);
            }
            else
            {
                throw new ConsoleError($"Nothing exists at : {_path}");
            }

            return new TextValue($"Removed {Path.GetFileName(Path.TrimEndingDirectorySeparator(_path))}");
        }

        public override void InvokeUndo(CommandInvocation invocation)
        {
            if (_path is null)
            {
                return;
            }

            if (_kind == PathKind.Directory)
            {
                Directory.CreateDirectory(_path);
            }
            else if (!File.Exists(_path))
            {
                File.WriteAllText(_path, _previousContents ?? string.Empty);
            }
        }
    }
}
