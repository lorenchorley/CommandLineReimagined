using Commands.Parser.SemanticTree;
using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace CommandLineReimagined.Web.Tokenisation;

public sealed record SemanticToken(string Text, string Kind);

/// <summary>
/// Walks a parsed command tree and emits a flat token stream, each token tagged with
/// the semantic role the grammar gave it.
///
/// This is the server half of the project's central claim: the command line is not a
/// string, it is a tree, so every run of characters on screen knows what it is and can
/// be coloured, hit-tested and interacted with. <see cref="SerialisationVisitor"/>
/// flattens the same tree back to text; this keeps the provenance.
/// </summary>
public sealed class TokenStreamVisitor : VisitorBase
{
    private readonly List<SemanticToken> _tokens = new();
    private string _kind = Kinds.Punctuation;

    public IReadOnlyList<SemanticToken> Tokens => _tokens;

    public static class Kinds
    {
        public const string Command = "command";
        public const string Flag = "flag";
        public const string String = "string";
        public const string Variable = "variable";
        public const string Identifier = "identifier";
        public const string Type = "type";
        public const string Attribute = "attribute";
        public const string Punctuation = "punctuation";
        public const string Whitespace = "whitespace";
        public const string NewLine = "newline";
    }

    public override void Append(string str)
    {
        if (str.Length == 0)
        {
            return;
        }

        // Spaces come from VisitorBase's private Space(), so they arrive carrying
        // whatever kind is currently in scope. Tag them separately instead.
        _tokens.Add(new SemanticToken(str, string.IsNullOrWhiteSpace(str) ? Kinds.Whitespace : _kind));
    }

    public override void Append(char c) => Append(c.ToString());

    public override void AppendNewLine() => _tokens.Add(new SemanticToken("\n", Kinds.NewLine));

    public override string GetResult() => string.Concat(_tokens.Select(t => t.Text));

    private Scope Using(string kind) => new(this, kind);

    private readonly struct Scope : IDisposable
    {
        private readonly TokenStreamVisitor _visitor;
        private readonly string _previous;

        public Scope(TokenStreamVisitor visitor, string kind)
        {
            _visitor = visitor;
            _previous = visitor._kind;
            visitor._kind = kind;
        }

        public void Dispose() => _visitor._kind = _previous;
    }

    // Each override sets the kind for the span of the base traversal, so nested
    // structure keeps its own tagging and punctuation emitted by the parent stays
    // punctuation.

    public override void VisitCommandName(CommandName commandName)
    {
        using (Using(Kinds.Command)) base.VisitCommandName(commandName);
    }

    public override void VisitCommandArgumentFlag(CommandArgumentFlag flag)
    {
        using (Using(Kinds.Flag)) base.VisitCommandArgumentFlag(flag);
    }

    public override void VisitStringConstant(StringConstant stringConstant)
    {
        using (Using(Kinds.String)) base.VisitStringConstant(stringConstant);
    }

    public override void VisitVariableReference(VariableReference variableReference)
    {
        using (Using(Kinds.Variable)) base.VisitVariableReference(variableReference);
    }

    public override void VisitVariableName(VariableName variableName)
    {
        using (Using(Kinds.Variable)) base.VisitVariableName(variableName);
    }

    public override void VisitIdentifier(Identifier identifier)
    {
        using (Using(Kinds.Identifier)) base.VisitIdentifier(identifier);
    }

    public override void VisitObjectType(ObjectType objectType)
    {
        using (Using(Kinds.Type)) base.VisitObjectType(objectType);
    }

    public override void VisitAttributeName(TagAttributeName attributeName)
    {
        using (Using(Kinds.Attribute)) base.VisitAttributeName(attributeName);
    }

    public override void VisitProperyName(ProperyName properyName)
    {
        using (Using(Kinds.Attribute)) base.VisitProperyName(properyName);
    }
}
