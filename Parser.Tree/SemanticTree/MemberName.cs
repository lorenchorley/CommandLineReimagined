using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    /// <summary>The <c>.size</c> of <c>$row.size</c>.</summary>
    /// <remarks>
    /// It carries its own dot, so that a visitor tagging tokens by role sees
    /// <c>.size</c> as one member token rather than a stray piece of punctuation inside
    /// a variable.
    /// </remarks>
    public record MemberName : IVisitable
    {
        public string Name { get; init; } = null!;

        public void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitMemberName(this);
        }
    }
}
