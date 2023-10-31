using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    public record Identifier : SimpleValue
    {
        public string Name { get; init; }

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitIdentifier(this);
        }
    }
}
