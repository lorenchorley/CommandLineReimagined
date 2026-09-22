using Commands.Parser.SemanticTree;
using System.Text;

namespace Isagri.Reporting.Quid.RequestFilters.SemanticTree;

public abstract class VisitorBase : ISemanticTreeVisitor
{
    private int indentationLevel = 0;
    private const char indentationChar = ' ';
    private const int indentationCharMultiplier = 4;
    private string initialIndentationString = "";
    private string indentationString = "";

    public bool UseIdentation { get; init; } = false;
    public bool UseNewLineOnPipe { get; init; } = false;

    public abstract string GetResult();
    public abstract void Append(string str);
    public abstract void Append(char c);
    public abstract void AppendNewLine();

    private void Indent()
    {
        indentationLevel++;
        RecalculateIndentationString();
    }

    private void Unindent()
    {
        indentationLevel--;
        RecalculateIndentationString();
    }

    private void RecalculateIndentationString()
    {
        indentationString = initialIndentationString + new string(indentationChar, indentationLevel * indentationCharMultiplier);
    }

    private void AddIndentation()
    {
        if (!UseIdentation)
        {
            return;
        }

        Append(indentationString);
    }

    private void NewLine(bool force = false)
    {
        if (!UseIdentation && !force)
        {
            return;
        }

        AppendNewLine();
    }

    public virtual void VisitAttributeName(TagAttributeName attributeName)
    {
        Append(attributeName.Name);
    }

    public virtual void VisitCommandArguments(CommandArguments commandArguments)
    {
        for (int i = 0; i < commandArguments.Arguments.Count; i++)
        {
            var arg = commandArguments.Arguments[i];

            if (i > 0)
            {
                Space();
            }

            arg.Accept(this);
        }
    }

    private void Space()
    {
        Append(' ');
    }

    public virtual void VisitCommandExpression(CommandExpression commandExpression)
    {
        commandExpression.Expression.Switch(
            function => function.Accept(this),
            commandName => commandName.Accept(this),
            commandExpressionCli => commandExpressionCli.Accept(this)
        );
    }

    public virtual void VisitCommandExpressionCli(CommandExpressionCli commandExpressionCli)
    {
        commandExpressionCli.Name.Accept(this);

        if (commandExpressionCli.Arguments.Arguments.Count > 0)
        {
            Space();
            commandExpressionCli.Arguments.Accept(this);
        }
    }

    public virtual void VisitCommandName(CommandName commandName)
    {
        Append(commandName.Name);
    }

    public virtual void VisitEmptyCommand(EmptyCommand emptyCommand)
    {
        Append("");
    }

    public virtual void VisitCommandArgumentFlag(CommandArgumentFlag flag)
    {
        Append('-');
        Append(flag.Name);
    }

    public virtual void VisitFunctionExpression(FunctionExpression functionExpression)
    {
        functionExpression.Id.Accept(this);
        Append('(');

        // Function arguments are comma separated, unlike CLI arguments which are
        // separated by spaces, so this cannot delegate to VisitCommandArguments. While
        // the comma production was unimplemented no call had more than one argument and
        // the difference never showed.
        var arguments = functionExpression.Arguments.Arguments;
        for (int i = 0; i < arguments.Count; i++)
        {
            if (i > 0)
            {
                Append(',');
                Space();
            }

            arguments[i].Accept(this);
        }

        Append(')');
    }

    public virtual void VisitIdentifier(Identifier identifier)
    {
        Append(identifier.Name);
    }

    public virtual void VisitObjectInstance(ObjectInstance objectInstance)
    {
        AddIndentation();
        Append('<');
        if (objectInstance.VariableName != null)
        {
            objectInstance.VariableName.Accept(this);
            Append('|');
        }

        objectInstance.ObjectType.Accept(this);

        if (objectInstance.Attributes.Attributes.Count > 0)
        {
            Space();
            objectInstance.Attributes.Accept(this);
        }

        if (!objectInstance.HasChildren)
        {
            Append("/>");
        }
        else
        {
            Append('>');

            Indent();
            //objectInstance.Children!.Tags.ForEach(c => c.Accept(this));
            for (int i = 0; i < objectInstance.Children!.Tags.Count; i++)
            {
                Tag child = objectInstance.Children.Tags[i];

                NewLine();

                child.Accept(this);
            }
            Unindent();
            
            NewLine();
            AddIndentation();

            Append("</");
            // Dispatch rather than appending the raw value, so a visitor that tags
            // tokens by role sees the closing tag name as an ObjectType too. The
            // serialised output is identical either way.
            objectInstance.ObjectType.Accept(this);
            Append('>');
        }
    }

    public virtual void VisitObjectType(ObjectType objectType)
    {
        Append(objectType.Value);
    }

    public virtual void VisitOptionalCommandArgument(OptionalCommandArgument optionalCommandArgument)
    {
        optionalCommandArgument.Name.Switch(
            flag => flag.Accept(this),
            name => name.Accept(this)
        );
        Append(':');
        Space();
        optionalCommandArgument.Value.Accept(this);
    }

    public virtual void VisitPipedCommandList(PipedCommandList pipedCommandList)
    {
        for (int i = 0; i < pipedCommandList.OrderedCommands.Count; i++)
        {
            if (i > 0)
            {
                Space();
                
                if (UseNewLineOnPipe)
                {
                    NewLine(true);
                }

                Append("| ");
                initialIndentationString = "  ";
            }

            pipedCommandList.OrderedCommands[i].Accept(this);
        }

        initialIndentationString = "";
    }

    public virtual void VisitRequiredCommandArgument(RequiredCommandArgument requiredCommandArgument)
    {
        requiredCommandArgument.Value.Accept(this);
    }

    public virtual void VisitTagAttribute(TagAttribute tagAttribute)
    {
        tagAttribute.Name.Accept(this);
        Append('=');
        tagAttribute.Value.Accept(this);
    }

    public virtual void VisitTagAttributes(TagAttributeList tagAttributes)
    {
        for (int i = 0; i < tagAttributes.Attributes.Count; i++)
        {
            // Without this separator `<t a=1 b=2/>` came back as `<t a=1b=2/>`, which is
            // not valid input, so a round trip was not idempotent.
            if (i > 0)
            {
                Space();
            }

            tagAttributes.Attributes[i].Accept(this);
        }
    }

    public virtual void VisitTagList(TagList tagList)
    {
        // Threw before, so any tree reached through a tag list could not be written out.
        foreach (var tag in tagList.Tags)
        {
            tag.Accept(this);
        }
    }

    public virtual void VisitVariableName(VariableName variableName)
    {
        Append(variableName.Name);
    }

    public virtual void VisitCommandArgumentValue(CommandArgumentValue commandArgumentValue)
    {
        commandArgumentValue.Value.Accept(this);
    }

    /// <summary>
    /// <c>name=value</c>, with no spaces: the spacing is what distinguishes an
    /// assignment from the three separate words <c>a</c>, <c>=</c>, <c>b</c>, so a
    /// round trip has to write it back tight.
    /// </summary>
    public virtual void VisitAssignmentArgument(AssignmentArgument assignmentArgument)
    {
        assignmentArgument.Name.Accept(this);
        Append('=');
        assignmentArgument.Value.Accept(this);
    }

    public virtual void VisitProperyName(ProperyName properyName)
    {
        Append(properyName.Name);
    }

    public virtual void VisitPropertyAssignment(PropertyAssignment propertyAssignment)
    {
        if (propertyAssignment.HasChildren)
        {
            Append('[');
            propertyAssignment.Name.Accept(this);
            Append(']');

            propertyAssignment.Children!.Accept(this);

            Append("[/");
            propertyAssignment.Name.Accept(this);
            Append(']');
            return;
        }

        Append('[');
        propertyAssignment.Name.Accept(this);
        Append('=');
        propertyAssignment.Value!.Accept(this);
        Append(']');
    }

    public virtual void VisitStringConstant(StringConstant stringConstant)
    {
        Append(stringConstant.QuoteString);
        Append(stringConstant.Value);
        Append(stringConstant.QuoteString);
    }

    public virtual void VisitVariableReference(VariableReference variableReference)
    {
        Append('$');
        variableReference.Name.Accept(this);

        foreach (var member in variableReference.Members)
        {
            member.Accept(this);
        }
    }

    /// <summary>
    /// A member carries its own dot, so <c>.size</c> is written and tagged as one thing.
    /// </summary>
    public virtual void VisitMemberName(MemberName memberName)
    {
        Append('.');
        Append(memberName.Name);
    }

    public virtual void VisitOperatorWord(OperatorWord operatorWord)
    {
        Append(operatorWord.Name);
    }

    public virtual void VisitComparisonExpression(ComparisonExpression comparisonExpression)
    {
        comparisonExpression.Left.Accept(this);
        Space();
        comparisonExpression.Operator.Accept(this);
        Space();
        comparisonExpression.Right.Accept(this);
    }

    public virtual void VisitBooleanExpression(BooleanExpression booleanExpression)
    {
        booleanExpression.Left.Accept(this);
        Space();
        booleanExpression.Operator.Accept(this);
        Space();
        booleanExpression.Right.Accept(this);
    }

    public virtual void VisitNotExpression(NotExpression notExpression)
    {
        notExpression.Operator.Accept(this);
        Space();
        notExpression.Operand.Accept(this);
    }

    /// <summary>
    /// A pipeline in parentheses. The parentheses are written back tight against the
    /// pipeline, because a space before one is what tells a nested pipeline from a
    /// function call.
    /// </summary>
    public virtual void VisitNestedPipeline(NestedPipeline nestedPipeline)
    {
        Append('(');
        nestedPipeline.Pipeline.Accept(this);
        Append(')');
    }

    public virtual void VisitVariableTag(VariableTag variableTag)
    {
        Append("<$");
        variableTag.Name.Accept(this);
        Append('>');
    }

    public virtual void VisitComponentType(ComponentType componentType)
    {
        Append(componentType.Value);
    }

    public virtual void VisitTagValue(TagValue tagValue)
    {
        tagValue.Tag.Accept(this);
    }

    public virtual void VisitComponentInstance(ComponentInstance componentInstance)
    {
        AddIndentation();
        Append('{');

        if (componentInstance.VariableName != null)
        {
            componentInstance.VariableName.Accept(this);
            Append('|');
        }

        componentInstance.ComponentType.Accept(this);

        if (componentInstance.Attributes.Attributes.Count > 0)
        {
            Space();
            componentInstance.Attributes.Accept(this);
        }

        if (!componentInstance.HasChildren)
        {
            Append("/}");
            return;
        }

        Append('}');

        Indent();
        foreach (var child in componentInstance.Children!.Tags)
        {
            NewLine();
            child.Accept(this);
        }
        Unindent();

        NewLine();
        AddIndentation();

        Append("{/");
        componentInstance.ComponentType.Accept(this);
        Append('}');
    }
}
