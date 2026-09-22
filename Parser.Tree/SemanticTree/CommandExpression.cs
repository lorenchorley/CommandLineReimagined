using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using OneOf;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    /// <summary>One stage of a pipeline.</summary>
    /// <remarks>
    /// Phase 5 adds three things around the command itself. A stage may be a whole
    /// pipeline in parentheses (decision 0023); it may be marked <c>try</c>, which turns
    /// its failure into a value; and it may carry a <c>??</c> default, used when it
    /// answers nothing. The two markers belong to the stage rather than to the pipeline,
    /// so <c>try cat x | set problem</c> binds the fault and <c>first (ls) ?? "none"</c>
    /// defaults what <c>first</c> returned.
    /// </remarks>
    public record CommandExpression : IVisitable
    {
        public OneOf<FunctionExpression, CommandExpressionCli, InstanceTag, NestedPipeline> Expression { get; init; }

        /// <summary>Written with <c>try</c> in front of it.</summary>
        public bool Try { get; init; }

        /// <summary>The operand after <c>??</c>, or null when there is none.</summary>
        public Value? Default { get; init; }

        public void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitCommandExpression(this);
        }
    }
}
