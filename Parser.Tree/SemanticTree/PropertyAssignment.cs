using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record PropertyAssignment : Tag, IVisitable
    {
        public ProperyName Name { get; init; } = new();

        /// <summary>Set by the <c>[name=value]</c> form.</summary>
        public SimpleValue? Value { get; init; }

        /// <summary>
        /// Set by the tag forms, <c>[name]&lt;x/&gt;[/name]</c> and <c>[name]=&lt;x/&gt;</c>.
        /// The grammar has always allowed a property to hold tags; the node only had room
        /// for a simple value, which is why those productions threw.
        /// </summary>
        public TagList? Children { get; init; }

        public bool HasChildren => Children != null && Children.Tags.Count > 0;

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitPropertyAssignment(this);
        }
    }
}
