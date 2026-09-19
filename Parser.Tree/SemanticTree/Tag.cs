using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public abstract record Tag : IVisitable
    {

        //public void Accept(ISemanticTreeVisitor visitor)
        //{
        //    visitor.VisitTag(this);
        //}
        public abstract void Accept(ISemanticTreeVisitor visitor);
    }
}
