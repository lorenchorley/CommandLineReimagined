using Commands.Parser.SemanticTree;

namespace Isagri.Reporting.Quid.RequestFilters.SemanticTree
{
    public interface IVisitable
    {
        void Accept(ISemanticTreeVisitor visitor);
    }
}
