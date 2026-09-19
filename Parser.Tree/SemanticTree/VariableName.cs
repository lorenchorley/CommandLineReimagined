using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    public record VariableName : IVisitable
    {
        private string _name;
        public string Name
        {
            get
            {
                return _name;
            }
            init
            {
                _name = value.TrimStart('$');
            }
        }

        public void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitVariableName(this);
        }
    }
}
