using Commands;
using Commands.Parser.SemanticTree;
using Terminal.Scoping;

namespace Terminal.Execution;

/// <summary>
/// Turns the arguments the grammar parsed into values bound to a command's declared
/// parameters.
/// </summary>
/// <remarks>
/// This replaces a conversion that recognised <see cref="StringConstant"/> and nothing
/// else, so every unquoted argument (which parses as an <see cref="Identifier"/>) threw
/// NotImplementedException and was then silently swallowed. Each value form is handled
/// here, and anything genuinely unsupported raises a <see cref="ConsoleError"/> naming
/// what it saw, so it reaches the user as a message instead of disappearing.
/// </remarks>
public sealed class ArgumentBinder
{
    /// <summary>
    /// Evaluates a parsed value to a runtime value, resolving variables against the scope.
    /// </summary>
    public RuntimeValue Evaluate(Value value, Scope scope)
    {
        switch (value)
        {
            case StringConstant text:
                return new TextValue(text.Value);

            // An unquoted word. The grammar cannot tell a bare name from a path or a
            // number, so it stays text and the command decides what it means.
            case Identifier identifier:
                return ParseNumberOrText(identifier.Name);

            case VariableReference reference:
            {
                string name = reference.Name.Name;
                var variable = scope.GetVariable(name)
                    ?? throw new ConsoleError($"Unknown variable : ${name}");

                return variable.Value;
            }

            case Constant constant:
                return new TextValue(constant.ToString() ?? string.Empty);

            default:
                throw new ConsoleError($"Unsupported argument value : {value.GetType().Name}");
        }
    }

    private static RuntimeValue ParseNumberOrText(string text) =>
        double.TryParse(text, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double number)
            ? new NumberValue(number)
            : new TextValue(text);

    /// <summary>
    /// Binds parsed arguments to declared parameters.
    /// </summary>
    /// <remarks>
    /// Positional arguments fill parameters in declaration order. Named arguments
    /// (<c>-flag value</c> or <c>name: value</c>) match by parameter name or flag
    /// wherever they appear. A bare flag with no value binds <c>true</c>, so
    /// <c>cmd -verbose</c> works.
    /// </remarks>
    public CommandParameterValue[] Bind(
        CommandArguments? arguments,
        CommandDefinition definition,
        RuntimeValue input,
        Scope scope)
    {
        var supplied = arguments?.Arguments ?? new List<CommandArgument>();
        var bound = new Dictionary<CommandParameter, CommandParameterValue>();
        var positional = new Queue<CommandArgument>();

        for (int i = 0; i < supplied.Count; i++)
        {
            var argument = supplied[i];

            switch (argument)
            {
                case OptionalCommandArgument named:
                {
                    string name = named.Name.Match(flag => flag.Name, identifier => identifier.Name);
                    var parameter = FindNamed(definition, name)
                        ?? throw new ConsoleError($"'{definition.Name}' has no argument named '{name}'.");

                    bound[parameter] = new CommandParameterValue
                    {
                        Parameter = parameter,
                        Value = Evaluate(named.Value, scope),
                    };
                    break;
                }

                // The grammar emits `-flag value` as a flag followed by a separate
                // argument, so the flag takes the next one as its value when there is
                // one. This is what the old ConsumeOneAhead placeholder was for. A flag
                // with nothing after it is a boolean switch.
                case CommandArgumentFlag flag:
                {
                    var parameter = FindNamed(definition, flag.Name)
                        ?? throw new ConsoleError($"'{definition.Name}' has no argument named '{flag.Name}'.");

                    RuntimeValue value;

                    if (i + 1 < supplied.Count && IsPlainValue(supplied[i + 1]))
                    {
                        value = Evaluate(ValueOf(supplied[i + 1]), scope);
                        i++;
                    }
                    else
                    {
                        value = new BooleanValue(true);
                    }

                    bound[parameter] = new CommandParameterValue
                    {
                        Parameter = parameter,
                        Value = value,
                    };
                    break;
                }

                default:
                    positional.Enqueue(argument);
                    break;
            }
        }

        foreach (var parameter in definition.Parameters)
        {
            if (bound.ContainsKey(parameter))
            {
                continue;
            }

            // Positional arguments fill parameters in declaration order, optional ones
            // included: `ls documents` and `progress 20 50` read the way a shell user
            // expects. Optional parameters only skip to their default when the
            // positional arguments have run out.
            if (positional.Count > 0)
            {
                var argument = positional.Dequeue();
                bound[parameter] = new CommandParameterValue
                {
                    Parameter = parameter,
                    Value = Evaluate(ValueOf(argument), scope),
                };
                continue;
            }

            // Nothing written out: fall back to the pipe, then to a default, then fail.
            if (parameter.AcceptsPipedInput && input is not EmptyValue)
            {
                bound[parameter] = new CommandParameterValue { Parameter = parameter, Value = input };
                continue;
            }

            if (parameter is OptionalCommandParameter optional)
            {
                bound[parameter] = new CommandParameterValue { Parameter = parameter, Value = optional.Default };
                continue;
            }

            throw new ConsoleError(
                $"'{definition.Name}' needs an argument for '{parameter.Name}'.");
        }

        if (positional.Count > 0)
        {
            // Counts what was supplied rather than what is left over, which read as
            // "takes 1 argument(s), but 1 more were given" for a two-argument call.
            int declared = definition.Parameters.Length;
            int given = declared + positional.Count;

            throw new ConsoleError(
                $"'{definition.Name}' takes {declared} argument{(declared == 1 ? "" : "s")}, " +
                $"but {given} were given.");
        }

        // Declaration order, so a command can still index Arguments[0] meaningfully.
        return definition.Parameters
                         .Where(bound.ContainsKey)
                         .Select(p => bound[p])
                         .ToArray();
    }

    /// <summary>An argument that carries a value, rather than naming one.</summary>
    private static bool IsPlainValue(CommandArgument argument) =>
        argument is RequiredCommandArgument or CommandArgumentValue;

    private static CommandParameter? FindNamed(CommandDefinition definition, string name)
    {
        foreach (var parameter in definition.Parameters)
        {
            if (string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return parameter;
            }

            if (parameter is OptionalCommandParameter optional &&
                string.Equals(optional.Flag, name, StringComparison.OrdinalIgnoreCase))
            {
                return parameter;
            }
        }

        return null;
    }

    private static Value ValueOf(CommandArgument argument) => argument switch
    {
        RequiredCommandArgument required => required.Value,
        CommandArgumentValue value => value.Value,
        OptionalCommandArgument optional => optional.Value,
        _ => throw new ConsoleError($"Unsupported argument : {argument.GetType().Name}"),
    };
}
