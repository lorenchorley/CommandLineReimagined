using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    /// <summary>
    /// A tag used where a value is expected, for <c>&lt;Value&gt; ::= &lt;InstanceTag&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Tags and values are separate hierarchies: InstanceTag descends from Tag, and a
    /// record cannot descend from Value as well. The grammar allows a tag anywhere a
    /// value is allowed, so this carries one across, which is what lets a command take a
    /// constructed object as an argument.
    /// </remarks>
    public record TagValue : Value
    {
        public InstanceTag Tag { get; init; } = null!;

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitTagValue(this);
        }
    }
}
