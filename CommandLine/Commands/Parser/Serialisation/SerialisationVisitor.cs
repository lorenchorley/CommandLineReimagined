using Commands.Parser.SemanticTree;
using System.Text;

namespace Isagri.Reporting.Quid.RequestFilters.SemanticTree;

public class SerialisationVisitor : ISemanticTreeVisitor
{
    private int indentationLevel = 0;
    private const char indentationChar = ' ';
    private const int indentationCharMultiplier = 4;
    private string initialIndentationString = "";
    private string indentationString = "";
    private StringBuilder _sb = new StringBuilder();

    public bool UseIdentation { get; init; } = false;
    public bool UseNewLineOnPipe { get; init; } = false;

    public string GetResult()
    {
        return _sb.ToString();
    }

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

        _sb.Append(indentationString);
    }

    private void NewLine(bool force = false)
    {
        if (!UseIdentation && !force)
        {
            return;
        }

        _sb.Append('\n');
    }

    public void VisitAttributeName(TagAttributeName attributeName)
    {
        throw new NotImplementedException();
    }

    public void VisitCommandArguments(CommandArguments commandArguments)
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
        _sb.Append(' ');
    }

    public void VisitCommandExpression(CommandExpression commandExpression)
    {
        commandExpression.Expression.Switch(
            function => function.Accept(this),
            commandName => commandName.Accept(this),
            commandExpressionCli => commandExpressionCli.Accept(this)
        );
    }

    public void VisitCommandExpressionCli(CommandExpressionCli commandExpressionCli)
    {
        commandExpressionCli.Name.Accept(this);

        if (commandExpressionCli.Arguments.Arguments.Count > 0)
        {
            Space();
            commandExpressionCli.Arguments.Accept(this);
        }
    }

    public void VisitCommandName(CommandName commandName)
    {
        _sb.Append(commandName.Name);
    }

    public void VisitEmptyCommand(EmptyCommand emptyCommand)
    {
        // Rien à faire
    }

    public void VisitCommandArgumentFlag(CommandArgumentFlag flag)
    {
        _sb.Append('-');
        _sb.Append(flag.Name);
    }

    public void VisitFunctionExpression(FunctionExpression functionExpression)
    {
        functionExpression.Id.Accept(this);
        _sb.Append('(');
        functionExpression.Arguments.Accept(this);
        _sb.Append(')');
    }

    public void VisitIdentifier(Identifier identifier)
    {
        _sb.Append(identifier.Name);
    }

    public void VisitObjectInstance(ObjectInstance objectInstance)
    {
        AddIndentation();
        _sb.Append('<');
        if (objectInstance.VariableName != null)
        {
            objectInstance.VariableName.Accept(this);
            _sb.Append('|');
        }

        objectInstance.ObjectType.Accept(this);

        if (objectInstance.Attributes.Attributes.Count > 0)
        {
            Space();
            objectInstance.Attributes.Accept(this);
        }

        if (!objectInstance.HasChildren)
        {
            _sb.Append("/>");
        }
        else
        {
            _sb.Append('>');

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

            _sb.Append("</");
            _sb.Append(objectInstance.ObjectType.Value);
            _sb.Append('>');
        }
    }

    public void VisitObjectType(ObjectType objectType)
    {
        _sb.Append(objectType.Value);
    }

    public void VisitOptionalCommandArgument(OptionalCommandArgument optionalCommandArgument)
    {
        optionalCommandArgument.Name.Switch(
            flag => flag.Accept(this),
            name => name.Accept(this)
        );
        _sb.Append(':');
        Space();
        optionalCommandArgument.Value.Accept(this);
    }

    public void VisitPipedCommandList(PipedCommandList pipedCommandList)
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

                _sb.Append("| ");
                initialIndentationString = "  ";
            }

            pipedCommandList.OrderedCommands[i].Accept(this);
        }

        initialIndentationString = "";
    }

    public void VisitRequiredCommandArgument(RequiredCommandArgument requiredCommandArgument)
    {
        requiredCommandArgument.Value.Accept(this);
    }

    public void VisitTagAttribute(TagAttribute tagAttribute)
    {
        _sb.Append(tagAttribute.Name.Name);
        _sb.Append('=');
        tagAttribute.Value.Accept(this);
    }

    public void VisitTagAttributes(TagAttributeList tagAttributes)
    {
        tagAttributes.Attributes.ForEach(a => a.Accept(this));
    }

    public void VisitTagList(TagList tagList)
    {
        throw new NotImplementedException();
    }

    public void VisitVariableName(VariableName variableName)
    {
        _sb.Append(variableName.Name);
    }

    public void VisitCommandArgumentValue(CommandArgumentValue commandArgumentValue)
    {
        commandArgumentValue.Value.Accept(this);
    }

    public void VisitProperyName(ProperyName properyName)
    {
        _sb.Append(properyName.Name);
    }

    public void VisitPropertyAssignment(PropertyAssignment propertyAssignment)
    {
        _sb.Append('[');
        propertyAssignment.Name.Accept(this);
        _sb.Append('=');
        propertyAssignment.Value.Accept(this);
        _sb.Append(']');
    }

    public void VisitStringConstant(StringConstant stringConstant)
    {
        _sb.Append(stringConstant.QuoteString);
        _sb.Append(stringConstant.Value);
        _sb.Append(stringConstant.QuoteString);
    }

    public void VisitVariableReference(VariableReference variableReference)
    {
        _sb.Append('$');
        variableReference.Name.Accept(this);
    }
}
