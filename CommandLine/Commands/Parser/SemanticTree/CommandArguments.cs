using System.Collections.Generic;

namespace CommandLineReimagine.Commands.Parser.SemanticTree
{
    public record CommandArguments : INode
    {
        public List<CommandArgument> Arguments { get; init; } = new();

        public CommandArguments()
        {
            
        }

        public CommandArguments(CommandArgument first)
        {
             Arguments.Add(first);
        }
    }
}
