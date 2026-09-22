using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    /// <summary>A whole pipeline written in parentheses where a value is expected.</summary>
    /// <remarks>
    /// <c>echo (ls | count)</c>. The grammar accepts one from Phase 3, because an
    /// operand is where it belongs and the expression grammar is what introduces
    /// operands; what running one means is Phase 5's business.
    /// </remarks>
    public record NestedPipeline : Value
    {
        public PipedCommandList Pipeline { get; init; } = null!;

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitNestedPipeline(this);
        }
    }
}
