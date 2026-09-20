using CommandLine.Modules;
using Terminal.Execution;

namespace Commands.Implementations
{
    public class PrintWorkingDirectory : CommandActionSync
    {
        private readonly PathModule _pathModule;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "pwd",
                Description: "The current directory",
                KeyWords: "where current directory path location",
                Parameters: new CommandParameter[] { },
                CommandActionType: typeof(PrintWorkingDirectory)
            );

        public PrintWorkingDirectory(PathModule pathModule)
        {
            _pathModule = pathModule;
        }

        public override RuntimeValue Invoke(CommandInvocation invocation) =>
            new PathValue(_pathModule.CurrentPath, PathKind.Directory);

        public override void InvokeUndo(CommandInvocation invocation)
        {
        }
    }
}
