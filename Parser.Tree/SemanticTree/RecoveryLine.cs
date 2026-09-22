using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    /// <summary>Pipelines joined by <c>else</c>: <c>cat notes.txt else echo "none"</c>.</summary>
    /// <remarks>
    /// Decision 0014. <c>else</c> binds looser than <c>|</c>, so each entry is a whole
    /// pipeline, and the line runs the next one only when the one before it failed,
    /// with the fault as its pipe input. A line with no <c>else</c> in it stays a plain
    /// <see cref="PipedCommandList"/>, which keeps every tree written before Phase 5
    /// exactly as it was.
    /// </remarks>
    public record RecoveryLine : RootNode
    {
        public List<PipedCommandList> Pipelines { get; init; } = new();

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitRecoveryLine(this);
        }
    }
}
