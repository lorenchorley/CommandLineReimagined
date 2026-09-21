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
    private bool _nextIdentifierIsCommandName;

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

    /// <summary>
    /// The name in <c>write(a, b)</c> names a command, exactly as <c>write a b</c> does.
    /// </summary>
    /// <remarks>
    /// It is an <see cref="Identifier"/> in the tree rather than a
    /// <see cref="CommandName"/>, so without this the function form coloured its
    /// command as an ordinary argument. The base visits the name first, so a one-shot
    /// flag is enough and the traversal does not have to be duplicated here.
    /// </remarks>
    public override void VisitFunctionExpression(FunctionExpression functionExpression)
    {
        _nextIdentifierIsCommandName = true;
        base.VisitFunctionExpression(functionExpression);
    }

    public override void VisitIdentifier(Identifier identifier)
    {
        string kind = _nextIdentifierIsCommandName ? Kinds.Command : Kinds.Identifier;
        _nextIdentifierIsCommandName = false;

        using (Using(kind)) base.VisitIdentifier(identifier);
    }

    public override void VisitObjectType(ObjectType objectType)
    {
        using (Using(Kinds.Type)) base.VisitObjectType(objectType);
    }

    public override void VisitComponentType(ComponentType componentType)
    {
        // A component's type reads the same way an object's does; without this it fell
        // through to punctuation and lost its colour.
        using (Using(Kinds.Type)) base.VisitComponentType(componentType);
    }

    public override void VisitVariableTag(VariableTag variableTag)
    {
        using (Using(Kinds.Variable)) base.VisitVariableTag(variableTag);
    }

    public override void VisitAttributeName(TagAttributeName attributeName)
    {
        using (Using(Kinds.Attribute)) base.VisitAttributeName(attributeName);
    }

    public override void VisitProperyName(ProperyName properyName)
    {
        using (Using(Kinds.Attribute)) base.VisitProperyName(properyName);
    }

    /// <summary>
    /// An assignment's name is an attribute name, not an ordinary identifier: it names
    /// a piece of data rather than being one.
    /// </summary>
    /// <remarks>
    /// The name is appended rather than dispatched. Dispatching reaches
    /// <see cref="VisitIdentifier"/>, which opens a scope of its own and would tag the
    /// name `identifier` whatever kind was in scope around it.
    /// </remarks>
    public override void VisitAssignmentArgument(AssignmentArgument assignmentArgument)
    {
        using (Using(Kinds.Attribute)) Append(assignmentArgument.Name.Name);
        Append('=');
        assignmentArgument.Value.Accept(this);
    }
}
