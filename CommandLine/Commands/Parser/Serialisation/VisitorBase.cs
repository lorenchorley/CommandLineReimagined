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

    public void VisitAttributeName(TagAttributeName attributeName)
    {
        Append(attributeName.Name);
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
        Append(' ');
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
        Append(commandName.Name);
    }

    public void VisitEmptyCommand(EmptyCommand emptyCommand)
    {
        Append("");
    }

    public void VisitCommandArgumentFlag(CommandArgumentFlag flag)
    {
        Append('-');
        Append(flag.Name);
    }

    public void VisitFunctionExpression(FunctionExpression functionExpression)
    {
        functionExpression.Id.Accept(this);
        Append('(');
        functionExpression.Arguments.Accept(this);
        Append(')');
    }

    public void VisitIdentifier(Identifier identifier)
    {
        Append(identifier.Name);
    }

    public void VisitObjectInstance(ObjectInstance objectInstance)
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
            Append(objectInstance.ObjectType.Value);
            Append('>');
        }
    }

    public void VisitObjectType(ObjectType objectType)
    {
        Append(objectType.Value);
    }

    public void VisitOptionalCommandArgument(OptionalCommandArgument optionalCommandArgument)
    {
        optionalCommandArgument.Name.Switch(
            flag => flag.Accept(this),
            name => name.Accept(this)
        );
        Append(':');
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

                Append("| ");
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
        tagAttribute.Name.Accept(this);
        Append('=');
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
        Append(variableName.Name);
    }

    public void VisitCommandArgumentValue(CommandArgumentValue commandArgumentValue)
    {
        commandArgumentValue.Value.Accept(this);
    }

    public void VisitProperyName(ProperyName properyName)
    {
        Append(properyName.Name);
    }

    public void VisitPropertyAssignment(PropertyAssignment propertyAssignment)
    {
        Append('[');
        propertyAssignment.Name.Accept(this);
        Append('=');
        propertyAssignment.Value.Accept(this);
        Append(']');
    }

    public void VisitStringConstant(StringConstant stringConstant)
    {
        Append(stringConstant.QuoteString);
        Append(stringConstant.Value);
        Append(stringConstant.QuoteString);
    }

    public void VisitVariableReference(VariableReference variableReference)
    {
        Append('$');
        variableReference.Name.Accept(this);
    }

    public void VisitVariableTag(VariableTag variableTag)
    {
        throw new NotImplementedException();
    }
}
