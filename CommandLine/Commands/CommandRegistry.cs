using CommandLineReimagined.Core;

namespace Terminal.Commands;

/// <summary>
/// What the shell can run, as specifications.
/// </summary>
/// <remarks>
/// Holds specifications rather than runnable commands. The session owns the runnable
/// ones and is the only thing that should be able to invoke one; everything in the
/// shell that used to hold an <c>ICommandAction</c> only ever wanted its name,
/// description and parameters.
/// </remarks>
public class CommandRegistry
{
    public CommandRegistry(IReadOnlyList<CommandSpec> commands)
    {
        Commands = commands;
    }

    public IReadOnlyList<CommandSpec> Commands { get; }
}
