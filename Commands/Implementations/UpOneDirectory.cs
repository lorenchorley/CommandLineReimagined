using CommandLine.Modules;

namespace CommandLineReimagined.Commands.Implementations
{
    public class UpOneDirectory : CommandAction
    {
        private readonly PathModule _pathModule;

        private string _previousFolder;

        public override CommandProfile Profile { get; } =
            new CommandProfile(
                Name: "up",
                Description: "",
                Parameters: new CommandParameter[]
                {
                },
                CommandActionType: typeof(UpOneDirectory)
            );

        public UpOneDirectory(PathModule pathModule)
        {
            _pathModule = pathModule;
        }

        public override void Execute(CommandParameterValue[] args, ConsoleOutScope scope)
        {
            var line = scope.NewLine();

            _previousFolder = _pathModule.CurrentFolder;
            _pathModule.Up();

            line.AddTextBlock("up", $"Moved up one to : {_pathModule.CurrentFolder}");
        }

        public override void Undo(CommandParameterValue[] args, ConsoleOutScope scope)
        {
            _pathModule.Enter(_previousFolder);
        }
    }
}
