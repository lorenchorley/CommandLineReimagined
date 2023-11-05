using Commands;

namespace Terminal.Commands;

public class CommandRegistry
{
    public List<CommandAction> Commands { get; } = new();

    public CommandRegistry(IEnumerable<CommandAction> commands)
    {
        Commands = commands.ToList();
    }
}
