using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record StringConstant : Constant, INode
    {
        public string Value { get; set; }
    }
}
