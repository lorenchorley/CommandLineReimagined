using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    /// <summary>The type name in a component tag, <c>{type/}</c>.</summary>
    public record ComponentType : IVisitable
    {
        public string Value { get; init; } = string.Empty;

        public void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitComponentType(this);
        }
    }
}
