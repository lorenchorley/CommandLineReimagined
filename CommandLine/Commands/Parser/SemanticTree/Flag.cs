using System.Collections.Generic;

namespace CommandLineReimagined.Commands.Parser.SemanticTree
{
    public record Flag : CommandArgument, INode
    {
        public string Name { get; init; }
    }
}
