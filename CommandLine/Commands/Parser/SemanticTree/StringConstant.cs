using System.Collections.Generic;

namespace CommandLineReimagined.Commands.Parser.SemanticTree
{
    public record StringConstant : Constant, INode
    {
        public string Value { get; set; }
    }
}
