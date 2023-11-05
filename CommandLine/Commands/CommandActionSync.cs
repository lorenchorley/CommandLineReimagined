using CommandLine.Modules;

namespace Commands
{
    public abstract class CommandActionSync : ICommandAction
    {
        public abstract CommandDefinition Profile { get; }
        public abstract void Invoke(CommandParameterValue[] args, CliBlock scope);
        public abstract void InvokeUndo(CommandParameterValue[] args, CliBlock scope);
    }
}
