using Terminal.Execution;
using Terminal.Variables;

namespace Commands.Implementations
{
    /// <summary>
    /// Binds a value to a variable: <c>set greeting hello</c>, or <c>ls | set files</c>.
    /// </summary>
    /// <remarks>
    /// Instance tags bind variables through their <c>&lt;name|type/&gt;</c> syntax, but
    /// that only covers objects. This is how a command's result gets a name, which is what
    /// makes <c>$files</c> usable in the next line. Undo restores the previous binding.
    /// </remarks>
    public class SetVariable : CommandActionSync
    {
        private string? _name;
        private Variable? _previous;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "set",
                Description: "Bind a value, or whatever was piped in, to a variable",
                KeyWords: "set variable assign bind let",
                Parameters: new CommandParameter[]
                {
                    new CommandParameter { Name = "name", Description = "The variable's name, without the $" },
                    new CommandParameter { Name = "value", Description = "The value", AcceptsPipedInput = true },
                },
                CommandActionType: typeof(SetVariable)
            );

        public override RuntimeValue Invoke(CommandInvocation invocation)
        {
            _name = invocation.Text("name");

            if (_name.Length == 0 || !_name.All(c => char.IsLetterOrDigit(c) || c == '_'))
            {
                throw new ConsoleError($"'{_name}' is not a valid variable name.");
            }

            var value = invocation.ValueOrInput("value");

            if (value is EmptyValue)
            {
                throw new ConsoleError($"'set' needs a value for '{_name}'.");
            }

            _previous = invocation.Scope.GetVariable(_name);
            invocation.Scope.SetVariable(new Variable(_name, value));

            return value;
        }

        public override void InvokeUndo(CommandInvocation invocation)
        {
            if (_name is null)
            {
                return;
            }

            if (_previous is null)
            {
                invocation.Scope.RemoveVariable(_name);
            }
            else
            {
                invocation.Scope.SetVariable(_previous);
            }
        }
    }
}
