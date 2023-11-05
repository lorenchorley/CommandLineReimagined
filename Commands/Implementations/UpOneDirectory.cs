using CommandLine.Modules;

namespace Commands.Implementations
{
    public class UpOneDirectory : CommandActionSync
    {
        private readonly PathModule _pathModule;

        private string _previousFolder;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "up",
                Description: "",
                KeyWords: "move",
                Parameters: new CommandParameter[]
                {
                },
                CommandActionType: typeof(UpOneDirectory)
            );

        public UpOneDirectory(PathModule pathModule)
        {
            _pathModule = pathModule;
        }

        public override void Invoke(CommandParameterValue[] args, CliBlock scope)
        {
            //var line = scope.NewLine();

            _previousFolder = _pathModule.CurrentFolder;
            _pathModule.Up();

            //line.AddTextBlock("up", $"Moved up one to : {_pathModule.CurrentFolder}");
        }

        public override void InvokeUndo(CommandParameterValue[] args, CliBlock scope)
        {
            _pathModule.Enter(_previousFolder);
        }
    }
}
