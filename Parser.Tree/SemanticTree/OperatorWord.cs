using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    /// <summary>
    /// One of the word operators: <c>eq ne gt ge lt le like has and or not</c>.
    /// </summary>
    /// <remarks>
    /// A node of its own rather than a string on the expression, so that a visitor which
    /// tags tokens by role can colour the word without knowing which expression it came
    /// from. Decision 0007 chose words over symbols; decision 0019 made them reserved.
    /// </remarks>
    public record OperatorWord : IVisitable
    {
        public string Name { get; init; } = null!;

        public void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitOperatorWord(this);
        }
    }
}
