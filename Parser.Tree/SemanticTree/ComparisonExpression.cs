using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    /// <summary><c>$row.size gt 100</c>: two operands and a comparison word.</summary>
    public record ComparisonExpression : ExpressionNode
    {
        public Value Left { get; init; } = null!;
        public OperatorWord Operator { get; init; } = null!;
        public Value Right { get; init; } = null!;

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitComparisonExpression(this);
        }
    }
}
