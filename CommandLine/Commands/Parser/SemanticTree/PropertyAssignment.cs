using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record PropertyAssignment : Tag, IVisitable
    {
        public ProperyName Name { get; init; }
        public SimpleValue Value { get; init; }

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitPropertyAssignment(this);
        }
    }
}
