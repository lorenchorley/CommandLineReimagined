using System.Collections.Generic;

namespace CommandLineReimagined.Commands.Parser.SemanticTree
{
    public record CommandName : INode
    {
        public string Name { get; set; }
    }
}
