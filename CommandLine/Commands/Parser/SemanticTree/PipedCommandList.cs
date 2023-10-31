using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record PipedCommandList : RootNode
    {
        public List<CommandExpression> OrderedCommands { get; init; } = new();
        public PipedCommandList()
        {
            
        }
        public PipedCommandList(CommandExpression first)
        {
            OrderedCommands.Add(first);
        }

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitPipedCommandList(this);
        }
    }
}
