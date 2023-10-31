using CommandLine.Modules;

namespace Commands.Implementations
{
    public class UpOneDirectory : CommandAction
    {
        private readonly PathModule _pathModule;

        private string _previousFolder;

        public override Command Profile { get; } =
            new Command(
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

        public override void Execute(CommandParameterValue[] args, ConsoleOutBlock scope)
        {
            //var line = scope.NewLine();

            _previousFolder = _pathModule.CurrentFolder;
            _pathModule.Up();

            //line.AddTextBlock("up", $"Moved up one to : {_pathModule.CurrentFolder}");
        }

        public override void Undo(CommandParameterValue[] args, ConsoleOutBlock scope)
        {
            _pathModule.Enter(_previousFolder);
        }
    }
}
