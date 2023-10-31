using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record TagAttributeList : IVisitable
    {
        public List<TagAttribute> Attributes { get; } = new();

        public TagAttributeList()
        {
            
        }

        public TagAttributeList(TagAttribute first)
        {
             Attributes.Add(first);
        }

        public void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitTagAttributes(this);
        }
    }
}
