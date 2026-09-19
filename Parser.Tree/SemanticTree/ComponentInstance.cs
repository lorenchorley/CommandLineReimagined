using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    /// <summary>
    /// A component tag: <c>{type/}</c>, <c>{name|type attr=v/}</c> or <c>{type}...{/}</c>.
    /// </summary>
    /// <remarks>
    /// The grammar has defined these throughout, but no node existed for them and every
    /// production threw, so the brace half of the language was unreachable. It mirrors
    /// <see cref="ObjectInstance"/> because the two tags differ only in their brackets
    /// and in what they mean to the ECS: angle brackets make entities, braces make
    /// components on them.
    /// </remarks>
    public record ComponentInstance : InstanceTag
    {
        public VariableName? VariableName { get; init; }

        public ComponentType ComponentType { get; init; } = new();

        public TagAttributeList Attributes { get; init; } = new();

        public TagList? Children { get; set; }

        public bool HasChildren => Children != null && Children.Tags.Count > 0;

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitComponentInstance(this);
        }
    }
}
