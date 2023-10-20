using System.Collections.Generic;

namespace CommandLineReimagined.Commands.Parser.SemanticTree
{
    public record PipedCommandList : INode
    {
        public List<CommandExpression> OrderedCommands { get; init; } = new();
        public PipedCommandList()
        {
            
        }
        public PipedCommandList(CommandExpression first)
        {
            OrderedCommands.Add(first);
        }
    }
}
