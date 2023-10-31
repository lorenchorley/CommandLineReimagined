using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record CommandName : IVisitable
    {
        public string Name { get; set; }

        public void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitCommandName(this);
        }
    }
}
