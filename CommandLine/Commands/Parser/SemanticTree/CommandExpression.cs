using System.Collections.Generic;

namespace CommandLineReimagined.Commands.Parser.SemanticTree
{
    public record CommandExpression : INode
    {
        public CommandName Id { get; init; }
        public CommandArguments Arguments { get; init; }
    }
}
