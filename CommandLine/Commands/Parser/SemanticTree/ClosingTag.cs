using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record ClosingTag
    {
        public ObjectType? TagObjectType { get; init; }
    }
}
