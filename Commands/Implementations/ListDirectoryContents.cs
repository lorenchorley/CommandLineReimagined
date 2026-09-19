using CommandLine.Modules;
using Terminal.Execution;

namespace Commands.Implementations
{
    /// <summary>
    /// Lists a directory.
    /// </summary>
    /// <remarks>
    /// Returns the entries as <see cref="PathValue"/>s instead of emitting buttons
    /// directly. The shell renders them, which keeps the interactive output (context
    /// menus, double-click actions) while letting the result flow into a pipe and
    /// letting this command be tested without a scene.
    /// </remarks>
    public class ListDirectoryContents : CommandActionSync
    {
        private readonly PathModule _pathModule;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "ls",
                Description: "List files and directories in a directory, the current directory by default",
                KeyWords: "show list dir",
                Parameters: new CommandParameter[]
                {
                    CommandParameter.Optional("path", "Directory to list; defaults to the current one"),
                },
                CommandActionType: typeof(ListDirectoryContents)
            );

        public ListDirectoryContents(PathModule pathModule)
        {
            _pathModule = pathModule;
        }

        public override RuntimeValue Invoke(CommandInvocation invocation)
        {
            string target = _pathModule.CurrentPath;

            if (invocation.TryValue("path", out var supplied) && supplied is not EmptyValue)
            {
                string requested = supplied.ToArgumentString();
                target = Path.IsPathRooted(requested)
                    ? requested
                    : Path.Combine(_pathModule.CurrentPath, requested);
            }

            if (!Directory.Exists(target))
            {
                throw new ConsoleError($"Directory does not exist : {target}");
            }

            var entries = new List<RuntimeValue>();

            if (!_pathModule.IsCurrentPathTheRoot())
            {
                entries.Add(new PathValue(target.GetFullPathOfOneDirectoryUp(), PathKind.Parent));
            }

            foreach (var folder in Directory.EnumerateDirectories(target))
            {
                entries.Add(new PathValue(folder, PathKind.Directory));
            }

            foreach (var file in Directory.EnumerateFiles(target))
            {
                entries.Add(new PathValue(file, PathKind.File));
            }

            return new ListValue(entries);
        }

        public override void InvokeUndo(CommandInvocation invocation)
        {
        }
    }
}
