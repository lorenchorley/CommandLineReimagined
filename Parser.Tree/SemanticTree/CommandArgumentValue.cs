using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record CommandArgumentValue : CommandArgument, IVisitable
    {
        public Value Value { get; init; }

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitCommandArgumentValue(this);
        }
    }
}
