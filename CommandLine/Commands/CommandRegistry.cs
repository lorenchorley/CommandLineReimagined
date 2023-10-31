using Commands;
using System.Diagnostics.CodeAnalysis;
using Terminal.Naming;

namespace Terminal.Commands;

public class CommandRegistry
{
    public List<Command> Commands { get; } = new();

}
