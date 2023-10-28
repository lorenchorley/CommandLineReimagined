using CommandLine.Modules;
using Console.Components;

namespace Commands.Implementations
{
    public class ListDirectoryContents : CommandAction
    {
        private readonly PathModule _pathModule;

        public override CommandProfile Profile { get; } =
            new CommandProfile(
                Name: "ls",
                Description: "List files and directories in a directory, the current directory by default",
                Parameters: new CommandParameter[]
                {
                },
                CommandActionType: typeof(ListDirectoryContents)
            );

        public ListDirectoryContents(PathModule pathModule)
        {
            _pathModule = pathModule;
        }

        public override void Execute(CommandParameterValue[] args, ConsoleOutScope scope)
        {
            LineComponent line = scope.NewLine();
            //line.AddTextBlock("ls title", $"Contenu du dossier {_pathModule.CurrentPath} :");

            // Nouvelle ligne après le titre
            line = scope.NewLine();

            // La command qui permet de remonter d'un dossier
            if (!_pathModule.IsCurrentPathTheRoot())
            {
                line.AddButton("ls up", $" up ")
                        .AddComponent<ContextMenuSource>(c => c.ContextMenuName = "PathNavigationContextMenu")
                        .AddComponent<DoubleClickAction>(c => c.ActionName = "Up");
            }

            // Les dossiers
            foreach (var folder in Directory.EnumerateDirectories(_pathModule.CurrentPath))
            {
                line.AddButton("ls folder", $" {Path.GetFileName(folder)}\\ ")
                    .AddComponent<PathInformation>(c => c.Path = folder)
                    .AddComponent<ContextMenuSource>(c => c.ContextMenuName = "PathNavigationContextMenu")
                    .AddComponent<DoubleClickAction>(c => c.ActionName = "Enter");
            }

            // Les fichiers
            foreach (var file in Directory.EnumerateFiles(_pathModule.CurrentPath))
            {
                line.AddButton("ls file", $" {Path.GetFileName(file)} ")
                    .AddComponent<PathInformation>(c => c.Path = file)
                    .AddComponent<ContextMenuSource>(c => c.ContextMenuName = "FileNavigationContextMenu")
                    .AddComponent<DoubleClickAction>(c => c.ActionName = "ShowContents");
            }

            // Nouvelle ligne à la fin
            line = scope.NewLine();
        }

        public override void Undo(CommandParameterValue[] args, ConsoleOutScope scope)
        {

        }
    }
}
