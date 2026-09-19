using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public abstract record RootNode : IVisitable
    {
        public abstract void Accept(ISemanticTreeVisitor visitor);
    }
}
