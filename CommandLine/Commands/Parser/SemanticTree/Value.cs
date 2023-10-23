using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public abstract record Value : CommandArgument, INode
    {
    }
}
