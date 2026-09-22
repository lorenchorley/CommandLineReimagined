using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record TagAttribute : IVisitable
    {
        public TagAttributeName Name { get; init; } = null!;
        public SimpleValue Value { get; init; } = null!;

        public void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitTagAttribute(this);
        }
    }
}
