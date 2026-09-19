using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    public record VariableReference : SimpleValue, IVisitable
    {
        public VariableName Name { get; init; }

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitVariableReference(this);
        }
    }
}
