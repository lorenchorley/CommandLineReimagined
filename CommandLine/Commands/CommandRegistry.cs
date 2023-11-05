using Commands;

namespace Terminal.Commands;

public class CommandRegistry
{
    public List<ICommandAction> Commands { get; } = new();

    public CommandRegistry(IEnumerable<ICommandAction> commands)
    {
        Commands = commands.ToList();
    }
}
