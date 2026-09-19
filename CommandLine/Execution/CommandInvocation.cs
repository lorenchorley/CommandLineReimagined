using Commands;
using Terminal.Scoping;

namespace Terminal.Execution;

/// <summary>
/// Everything one command execution needs: its bound arguments, the value piped into it,
/// somewhere to write progress, the scope it runs in, and a way to be cancelled.
/// </summary>
/// <remarks>
/// Replaces the old <c>(CommandParameterValue[] args, CliBlock scope)</c> pair. Passing a
/// single object means adding pipe input, scope and cancellation did not have to change
/// every command signature again.
/// </remarks>
public sealed class CommandInvocation
{
    public CommandInvocation(
        CommandDefinition definition,
        IReadOnlyList<CommandParameterValue> arguments,
        RuntimeValue input,
        ICommandOutput output,
        Scope scope,
        CancellationToken cancellation = default)
    {
        Definition = definition;
        Arguments = arguments;
        Input = input;
        Output = output;
        Scope = scope;
        Cancellation = cancellation;
    }

    public CommandDefinition Definition { get; }

    public IReadOnlyList<CommandParameterValue> Arguments { get; }

    /// <summary>The previous command's result, or <see cref="RuntimeValue.Empty"/> at the head of a pipe.</summary>
    public RuntimeValue Input { get; }

    public ICommandOutput Output { get; }

    public Scope Scope { get; }

    public CancellationToken Cancellation { get; }

    /// <summary>
    /// The value of a declared parameter. The binder has already checked that required
    /// parameters are present, so this throws rather than returning null: a command
    /// asking for a parameter it declared should not have to null-check.
    /// </summary>
    public RuntimeValue Value(string parameterName)
    {
        foreach (var argument in Arguments)
        {
            if (string.Equals(argument.Parameter?.Name, parameterName, StringComparison.OrdinalIgnoreCase))
            {
                return argument.Value;
            }
        }

        throw new ConsoleError($"'{Definition.Name}' has no argument named '{parameterName}'.");
    }

    public string Text(string parameterName) => Value(parameterName).ToArgumentString();

    public bool TryValue(string parameterName, out RuntimeValue value)
    {
        foreach (var argument in Arguments)
        {
            if (string.Equals(argument.Parameter?.Name, parameterName, StringComparison.OrdinalIgnoreCase))
            {
                value = argument.Value;
                return true;
            }
        }

        value = RuntimeValue.Empty;
        return false;
    }

    /// <summary>
    /// The argument for a parameter, falling back to whatever was piped in.
    /// </summary>
    /// <remarks>
    /// This is what makes <c>ls | echo</c> work: a command whose argument was not written
    /// out takes the pipe's value instead, which is the usual shell convention.
    /// </remarks>
    public RuntimeValue ValueOrInput(string parameterName) =>
        TryValue(parameterName, out var value) && value is not EmptyValue ? value : Input;
}
