using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    /// <summary>
    /// An expression written in argument position: a comparison, a boolean combination
    /// or a negation.
    /// </summary>
    /// <remarks>
    /// It descends from <see cref="Value"/> rather than standing beside it, because the
    /// grammar admits an expression exactly where it admits a value and the difference
    /// is decided by what was written rather than by where it was written. A single
    /// operand with no operator around it is not one of these: it stays the plain node
    /// it always was, so every line written before expressions existed still parses to
    /// the same tree.
    /// </remarks>
    public abstract record ExpressionNode : Value
    {
    }
}
