using System.Collections.Generic;

namespace CommandLineReimagine.Commands.Parser.SemanticTree
{
    public record CommandName : INode
    {
        public string Name { get; set; }
    }
}
