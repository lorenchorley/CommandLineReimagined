using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    /// <summary>
    /// A command argument of the form <c>name=value</c>, written with no spaces around
    /// the <c>=</c>: <c>attr notes.txt tag=work</c>.
    /// </summary>
    /// <remarks>
    /// This is data, not parameter binding. Decision 0017 keeps <c>name: value</c> as
    /// the only way to bind a declared parameter by name, so that a command such as
    /// <c>attr</c> can take attribute names it has never heard of without the binder
    /// rejecting them. The two notations look alike and are told apart by one
    /// character, which is the price of letting a tag attribute and a file attribute
    /// share a spelling.
    /// </remarks>
    public record AssignmentArgument : CommandArgument
    {
        public Identifier Name { get; init; } = null!;

        public Value Value { get; init; } = null!;

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitAssignmentArgument(this);
        }
    }
}
