using CommandLine.Modules;

namespace CommandLineReimagined.Commands
{
    public abstract class CommandAction
    {
        public abstract CommandProfile Profile { get; }
        public abstract void Execute(CommandParameterValue[] args, ConsoleOutScope scope);
        public abstract void Undo(CommandParameterValue[] args, ConsoleOutScope scope);
    }
}
