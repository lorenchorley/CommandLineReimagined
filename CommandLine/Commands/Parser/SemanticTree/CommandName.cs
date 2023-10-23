using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record CommandName : INode
    {
        public string Name { get; set; }
    }
}
