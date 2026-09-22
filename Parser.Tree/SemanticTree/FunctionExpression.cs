using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using OneOf;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record FunctionExpression : IVisitable
    {
        public Identifier Id { get; init; } = null!;
        public CommandArguments Arguments { get; init; } = null!;

        public void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitFunctionExpression(this);
        }
    }
}
