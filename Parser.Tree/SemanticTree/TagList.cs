using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record TagList : IVisitable
    {
        public List<Tag> Tags { get; init; } = new();

        public TagList()
        {
            
        }

        public TagList(Tag first)
        {
             Tags.Add(first);
        }

        public void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitTagList(this);
        }
    }
}
