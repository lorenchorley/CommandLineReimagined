using CommandLine.Modules;

namespace Commands
{
    public interface ICommandAction
    {
        CommandDefinition Profile { get; }
    }
}
