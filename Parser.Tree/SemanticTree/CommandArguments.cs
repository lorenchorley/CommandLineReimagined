using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record CommandArguments : IVisitable
    {
        public List<CommandArgument> Arguments { get; init; } = new();

        public CommandArguments()
        {
            
        }

        public CommandArguments(CommandArgument first)
        {
             Arguments.Add(first);
        }

        public void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitCommandArguments(this);
        }
    }
}
