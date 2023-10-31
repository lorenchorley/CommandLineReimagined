using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record RequiredCommandArgument : CommandArgument
    {
        public Value Value { get; init; }

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitRequiredCommandArgument(this);
        }
    }
}
