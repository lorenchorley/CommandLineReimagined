using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    public abstract record Value : IVisitable
    {
        public abstract void Accept(ISemanticTreeVisitor visitor);
    }
}
