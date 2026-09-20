using Terminal.Execution;

namespace Commands.Implementations
{
    /// <summary>
    /// Lists every variable in scope, one per output line, and returns their values.
    /// </summary>
    public class ListVariables : CommandActionSync
    {
        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "vars",
                Description: "List the variables in scope",
                KeyWords: "variables list show scope",
                Parameters: new CommandParameter[] { },
                CommandActionType: typeof(ListVariables)
            );

        public override RuntimeValue Invoke(CommandInvocation invocation)
        {
            var variables = invocation.Scope.AllVariables();

            if (variables.Count == 0)
            {
                invocation.Output.NewLine().Write("vars", "No variables. Try: set greeting hello");
                return RuntimeValue.Empty;
            }

            foreach (var variable in variables)
            {
                invocation.Output.NewLine().Write("vars", $"${variable.Name} = {variable.Value.ToDisplayString()}");
            }

            return new ListValue(variables.Select(v => v.Value).ToList());
        }

        public override void InvokeUndo(CommandInvocation invocation)
        {
        }
    }
}
