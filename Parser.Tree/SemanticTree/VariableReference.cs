using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record VariableReference : SimpleValue, IVisitable
    {
        public VariableName Name { get; init; } = null!;

        /// <summary>
        /// The members read off the variable, in the order they were written:
        /// <c>$row.size</c> has one, <c>$row</c> has none.
        /// </summary>
        /// <remarks>
        /// Decision 0008: a predicate names its row explicitly, so reading a column is
        /// a member access on an ordinary variable rather than a bare name the reader
        /// has to recognise.
        /// </remarks>
        public List<MemberName> Members { get; init; } = new();

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitVariableReference(this);
        }
    }
}
