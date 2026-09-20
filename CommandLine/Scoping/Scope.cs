using Commands;
using Terminal.Naming;
using Terminal.Variables;

namespace Terminal.Scoping
{
    public class Scope
    {
        public Namespace Namespace { get; init; }
        public Scope? Parent { get; init; }

        public Dictionary<string, Variable> Variables { get; } = new();
        public Dictionary<string, CommandDefinition> Commands { get; } = new();
        public Dictionary<string, TypeSystem.Type> Types { get; } = new();

        public Variable? GetVariable(string name)
        {
            if (Variables.TryGetValue(name, out Variable? variable))
            {
                return variable;
            }

            return Parent?.GetVariable(name);
        }

        /// <summary>
        /// Binds a variable in this scope, shadowing any of the same name in a parent.
        /// </summary>
        public void SetVariable(Variable variable) => Variables[variable.Name] = variable;

        /// <summary>Unbinds a variable from this scope. Returns whether it was bound here.</summary>
        public bool RemoveVariable(string name) => Variables.Remove(name);

        /// <summary>Every variable visible from this scope, innermost binding winning.</summary>
        public IReadOnlyList<Variable> AllVariables()
        {
            var seen = new Dictionary<string, Variable>();

            for (Scope? scope = this; scope is not null; scope = scope.Parent)
            {
                foreach (var variable in scope.Variables.Values)
                {
                    seen.TryAdd(variable.Name, variable);
                }
            }

            return seen.Values.OrderBy(v => v.Name, StringComparer.Ordinal).ToList();
        }

        public CommandDefinition? GetCommand(string name)
        {
            if (Commands.TryGetValue(name, out CommandDefinition? command))
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
