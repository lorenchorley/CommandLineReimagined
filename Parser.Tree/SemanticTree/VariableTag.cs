using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    public record VariableTag : InstanceTag
    {
        public VariableName Name { get; init; }

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitVariableTag(this);
        }
    }
}
