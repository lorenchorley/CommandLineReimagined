using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using OneOf;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record CommandExpression : IVisitable
    {
        public OneOf<FunctionExpression, CommandExpressionCli, InstanceTag> Expression { get; init; }

        public void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitCommandExpression(this);
        }
    }
}
