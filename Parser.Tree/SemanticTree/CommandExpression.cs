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
    /// so <c>try read x | set problem</c> binds the fault and <c>first (ls) ?? "none"</c>
    /// defaults what <c>first</c> returned.
    ///
    /// Phase 8 adds a fifth form (decision 0032): a variable reference, with or without
    /// members, may stand as a stage, so <c>$files | count</c> and <c>$maybe ?? "x"</c>
    /// are lines. The node is the same <see cref="VariableReference"/> an argument
    /// holds, because the two are read the same way; only where it stands differs.
    /// </remarks>
    public record CommandExpression : IVisitable
    {
        public OneOf<FunctionExpression, CommandExpressionCli, InstanceTag, NestedPipeline, VariableReference> Expression { get; init; }

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
