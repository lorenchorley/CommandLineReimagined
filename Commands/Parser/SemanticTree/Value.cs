using System.Collections.Generic;

namespace CommandLineReimagine.Commands.Parser.SemanticTree
{
    public abstract record Value : CommandArgument, INode
    {
    }
}
