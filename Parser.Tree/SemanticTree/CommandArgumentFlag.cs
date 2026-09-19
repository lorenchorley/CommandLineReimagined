using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Commands.Parser.SemanticTree
{
    public record CommandArgumentFlag : CommandArgument
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
                _name = value.TrimStart('-');
            }
        }

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitCommandArgumentFlag(this);
        }
    }
}
