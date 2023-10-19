using System.Collections.Generic;

namespace CommandLineReimagine.Commands.Parser.SemanticTree
{
    public record Flag : CommandArgument, INode
    {
        public string Name { get; init; }
    }
}
