using CommandLine.Modules;

namespace Commands
{
    public abstract class CommandAction
    {
        public abstract Command Profile { get; }
        public abstract void Execute(CommandParameterValue[] args, ConsoleOutBlock scope);
        public abstract void Undo(CommandParameterValue[] args, ConsoleOutBlock scope);
    }
}
