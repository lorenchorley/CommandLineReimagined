using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record CommandExpressionCli : IVisitable
    {
        public CommandName Name { get; init; }
        public CommandArguments Arguments { get; init; }

        public void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitCommandExpressionCli(this);
        }
    }
}
