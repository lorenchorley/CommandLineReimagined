using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    public record TagAttributeName : IVisitable
    {
        public string Name { get; init; }

        public void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitAttributeName(this);
        }
    }
}
