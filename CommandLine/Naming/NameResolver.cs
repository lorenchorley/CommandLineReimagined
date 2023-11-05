using Commands;
using Commands.Parser.SemanticTree;
using OneOf;
using Terminal.FileSystem;
using Terminal.Scoping;
using Terminal.Variables;

namespace Terminal.Naming
{
    public class UnresolvedName
    {
        private UnresolvedName()
        {
        }

        public static readonly UnresolvedName Instance = new();
    }

    public class NameResolver
    {
        public OneOf<UnresolvedName, Variable, CommandDefinition, FileSystemObject, TypeSystem.Type, Constant> Resolve(string name, ResolvableNameType types, Scope scope)
        {
            if (types == ResolvableNameType.None)
            {
                return UnresolvedName.Instance;
            }

            if (types.HasFlag(ResolvableNameType.Variable))
            {
                var variable = scope.GetVariable(name);
                if (variable != null)
                {
                    return variable;
                }
            }

            if (types.HasFlag(ResolvableNameType.FileSystemObject))
            {
                // TODO
                // From current directory, find file or directory with the given name
            }

            if (types.HasFlag(ResolvableNameType.Command))
            {
                var command = scope.GetCommand(name);
                if (command != null)
                {
                    return command;
                }
            }

            if (types.HasFlag(ResolvableNameType.Type))
            {
                var type = scope.GetType(name);
                if (type != null)
                {
                    return type;
                }
            }

            if (types.HasFlag(ResolvableNameType.Literal))
            {
                // TODO useful ?
            }

            return UnresolvedName.Instance;
        }
    }
}
