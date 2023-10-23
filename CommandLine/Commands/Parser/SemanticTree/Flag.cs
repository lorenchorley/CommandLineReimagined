using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record Flag : CommandArgument, INode
    {
        public string Name { get; init; }
    }
}
