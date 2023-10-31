using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record ObjectInstance : InstanceTag
    {
        public VariableName? VariableName { get; init; }
        public ObjectType ObjectType { get; init; }
        public TagAttributeList Attributes { get; init; }
        public TagList? Children { get; set; }

        public bool HasChildren
        {
            get
            {
                return Children != null && Children.Tags.Count > 0;
            }
        }

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitObjectInstance(this);
        }
    }
}
