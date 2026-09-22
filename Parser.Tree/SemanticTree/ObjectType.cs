using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    public record ObjectType : IVisitable
    {
        public string Value { get; init; } = null!;

        public void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitObjectType(this);
        }
    }
}
