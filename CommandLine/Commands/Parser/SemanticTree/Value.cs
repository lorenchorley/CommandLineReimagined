using System.Collections.Generic;

namespace CommandLineReimagined.Commands.Parser.SemanticTree
{
    public abstract record Value : CommandArgument, INode
    {
    }
}
