using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using OneOf;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record OptionalCommandArgument : CommandArgument
    {
        public OneOf<CommandArgumentFlag, Identifier> Name { get; init; }
        public Value Value { get; init; }

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitOptionalCommandArgument(this);
        }
    }
}
