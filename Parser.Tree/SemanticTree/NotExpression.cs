using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    /// <summary><c>not $row.kind eq folder</c>.</summary>
    public record NotExpression : ExpressionNode
    {
        public OperatorWord Operator { get; init; } = null!;
        public Value Operand { get; init; } = null!;

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitNotExpression(this);
        }
    }
}
