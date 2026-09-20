using CommandLine.Modules;
using Terminal.Execution;

namespace Commands.Implementations
{
    public class ReadFile : CommandActionSync
    {
        private readonly PathModule _pathModule;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "cat",
                Description: "Read a file and return its text",
                KeyWords: "read print show file contents type",
                Parameters: new CommandParameter[]
                {
                    // Piped input means `ls | cat` style chains and `echo readme.txt | cat` work.
                    new CommandParameter { Name = "path", Description = "The file to read", AcceptsPipedInput = true },
                },
                CommandActionType: typeof(ReadFile)
            );

        public ReadFile(PathModule pathModule)
        {
            _pathModule = pathModule;
        }

        public override RuntimeValue Invoke(CommandInvocation invocation)
        {
            string path = _pathModule.Resolve(invocation.ValueOrInput("path").ToArgumentString());

            if (Directory.Exists(path))
            {
                throw new ConsoleError($"That is a directory, not a file : {path}");
            }

            if (!File.Exists(path))
            {
                throw new ConsoleError($"File does not exist : {path}");
            }

            return new TextValue(File.ReadAllText(path));
        }

        public override void InvokeUndo(CommandInvocation invocation)
        {
        }
    }
}
