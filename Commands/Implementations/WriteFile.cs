using CommandLine.Modules;
using Terminal.Execution;

namespace Commands.Implementations
{
    /// <summary>
    /// Writes text to a file, creating it or replacing what was there.
    /// </summary>
    /// <remarks>
    /// The text can be piped in, so <c>echo hello | write note.txt</c> and
    /// <c>cat a.txt | write b.txt</c> both work. Undo puts the previous contents back,
    /// or removes the file when it did not exist before.
    /// </remarks>
    public class WriteFile : CommandActionSync
    {
        private readonly PathModule _pathModule;

        private string? _path;
        private string? _previousContents;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "write",
                Description: "Write text to a file, replacing its contents",
                KeyWords: "write save file create text",
                Parameters: new CommandParameter[]
                {
                    new CommandParameter { Name = "path", Description = "The file to write" },
                    new CommandParameter { Name = "text", Description = "What to write", AcceptsPipedInput = true },
                },
                CommandActionType: typeof(WriteFile)
            );

        public WriteFile(PathModule pathModule)
        {
            _pathModule = pathModule;
        }

        public override RuntimeValue Invoke(CommandInvocation invocation)
        {
            _path = _pathModule.Resolve(invocation.Text("path"));
            string text = invocation.ValueOrInput("text").ToDisplayString();

            if (Directory.Exists(_path))
            {
                throw new ConsoleError($"That is a directory, not a file : {_path}");
            }

            string? directory = Path.GetDirectoryName(_path);
            if (directory is not null && !Directory.Exists(directory))
            {
                throw new ConsoleError($"Directory does not exist : {directory}");
            }

            _previousContents = File.Exists(_path) ? File.ReadAllText(_path) : null;
            File.WriteAllText(_path, text);

            return new PathValue(_path, PathKind.File);
        }

        public override void InvokeUndo(CommandInvocation invocation)
        {
            if (_path is null)
            {
                return;
            }

            if (_previousContents is null)
            {
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }
            }
            else
            {
                File.WriteAllText(_path, _previousContents);
            }
        }
    }
}
