using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record EmptyCommand : RootNode, IVisitable
    {
        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitEmptyCommand(this);
        }
    }
}
