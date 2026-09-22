using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    /// <summary><c>a and b</c> or <c>a or b</c>.</summary>
    /// <remarks>
    /// One node for both words, because they differ only in the word: the grammar gives
    /// <c>and</c> the tighter binding, and the tree records that as its shape.
    /// </remarks>
    public record BooleanExpression : ExpressionNode
    {
        public Value Left { get; init; } = null!;
        public OperatorWord Operator { get; init; } = null!;
        public Value Right { get; init; } = null!;

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitBooleanExpression(this);
        }
    }
}
