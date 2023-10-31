using Commands;
using Terminal.Variables;

namespace Terminal.Naming
{
    public class Scope
    {
        public Namespace? Namespace { get; init; }
        public Scope? Parent { get; init; }

        public Dictionary<string, Variable> Variables { get; } = new();
        public Dictionary<string, Command> Commands { get; } = new();
        public Dictionary<string, TypeSystem.Type> Types { get; } = new();

        public Variable? GetVariable(string name)
        {
            if (Variables.TryGetValue(name, out Variable? variable))
            {
                return variable;
            }

            return Parent?.GetVariable(name);
        }

        public Command? GetCommand(string name)
        {
            if (Commands.TryGetValue(name, out Command? command))
            {
                return command;
            }

            return Parent?.GetCommand(name);
        }

        public TypeSystem.Type? GetType(string name)
        {
            if (Types.TryGetValue(name, out TypeSystem.Type? type))
            {
                return type;
            }

            return Parent?.GetType(name);
        }
    }
}
