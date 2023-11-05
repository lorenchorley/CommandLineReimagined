using Commands.Parser.SemanticTree;

namespace Isagri.Reporting.Quid.RequestFilters.SemanticTree
{
    public interface ISemanticTreeVisitor
    {
        void VisitAttributeName(TagAttributeName attributeName);
        void VisitCommandArguments(CommandArguments commandArguments);
        void VisitCommandArgumentValue(CommandArgumentValue commandArgumentValue);
        void VisitCommandExpression(CommandExpression commandExpression);
        void VisitCommandExpressionCli(CommandExpressionCli commandExpressionCli);
        void VisitCommandName(CommandName commandName);
        void VisitEmptyCommand(EmptyCommand emptyCommand);
        void VisitCommandArgumentFlag(CommandArgumentFlag flag);
        void VisitFunctionExpression(FunctionExpression functionExpression);
        void VisitIdentifier(Identifier identifier);
        void VisitObjectInstance(ObjectInstance objectInstance);
        void VisitObjectType(ObjectType objectType);
        void VisitOptionalCommandArgument(OptionalCommandArgument optionalCommandArgument);
        void VisitPipedCommandList(PipedCommandList pipedCommandList);
        void VisitRequiredCommandArgument(RequiredCommandArgument requiredCommandArgument);
        //void VisitTag(Tag tag);
        void VisitTagAttribute(TagAttribute tagAttribute);
        void VisitTagAttributes(TagAttributeList tagAttributes);
        void VisitTagList(TagList tagList);
        void VisitVariableName(VariableName variableName);
        void VisitProperyName(ProperyName properyName);
        void VisitPropertyAssignment(PropertyAssignment propertyAssignment);
        void VisitStringConstant(StringConstant stringConstant);
        void VisitVariableReference(VariableReference variableReference);
        void VisitVariableTag(VariableTag variableTag);
    }
}
