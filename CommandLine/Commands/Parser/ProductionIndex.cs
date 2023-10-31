﻿//Generated by the GOLD Parser Builder

namespace Commands.Parser;

public enum ProductionIndex
{
    @Constant_Stringliteral2 = 0,              // <Constant> ::= 'StringLiteral2'
    @Constant_Stringliteral3 = 1,              // <Constant> ::= 'StringLiteral3'
    @Id_Identifier = 2,                        // <ID> ::= Identifier
    @Objecttype_Identifier = 3,                // <ObjectType> ::= Identifier
    @Variablename_Identifier = 4,              // <VariableName> ::= Identifier
    @Typename_Identifier = 5,                  // <TypeName> ::= Identifier
    @Propertyname_Identifier = 6,              // <PropertyName> ::= Identifier
    @Tagattributename_Identifier = 7,          // <TagAttributeName> ::= Identifier
    @Componenttype_Identifier = 8,             // <ComponentType> ::= Identifier
    @Flag_Flagidentifier = 9,                  // <Flag> ::= FlagIdentifier
    @Program = 10,                             // <Program> ::= <PipedCommandList>
    @Program2 = 11,                            // <Program> ::= 
    @Pipedcommandlist_Pipe = 12,               // <PipedCommandList> ::= <PipedCommandList> '|' <CommandExpression>
    @Pipedcommandlist = 13,                    // <PipedCommandList> ::= <CommandExpression>
    @Commandexpression = 14,                   // <CommandExpression> ::= <FunctionExpression>
    @Commandexpression2 = 15,                  // <CommandExpression> ::= <CommandExpression_CLINotation>
    @Commandexpression3 = 16,                  // <CommandExpression> ::= <IndividualCLIValue>
    @Functionexpression_Lparen_Rparen = 17,    // <FunctionExpression> ::= <ID> '(' <FunctionArgumentList> ')'
    @Functionexpression_Lparen_Rparen2 = 18,   // <FunctionExpression> ::= <ID> '(' ')'
    @Functionargumentlist_Comma = 19,          // <FunctionArgumentList> ::= <FunctionArgumentList> ',' <FunctionArgument>
    @Functionargumentlist = 20,                // <FunctionArgumentList> ::= <FunctionArgument>
    @Functionargument = 21,                    // <FunctionArgument> ::= <RequiredArgument>
    @Functionargument2 = 22,                   // <FunctionArgument> ::= <OptionalArgument>
    @Requiredargument = 23,                    // <RequiredArgument> ::= <Value>
    @Optionalargument_Colon = 24,              // <OptionalArgument> ::= <ID> ':' <Value>
    @Commandexpression_clinotation = 25,       // <CommandExpression_CLINotation> ::= <ID> <CommandArgumentList>
    @Commandargumentlist = 26,                 // <CommandArgumentList> ::= <CommandArgumentList> <CommandArgument>
    @Commandargumentlist2 = 27,                // <CommandArgumentList> ::= 
    @Commandargument = 28,                     // <CommandArgument> ::= <Flag>
    @Commandargument2 = 29,                    // <CommandArgument> ::= <Value>
    @Value = 30,                               // <Value> ::= <SimpleValue>
    @Value2 = 31,                              // <Value> ::= <InstanceTag>
    @Simplevalue = 32,                         // <SimpleValue> ::= <Constant>
    @Simplevalue2 = 33,                        // <SimpleValue> ::= <VariableReference>
    @Simplevalue3 = 34,                        // <SimpleValue> ::= <ID>
    @Individualclivalue = 35,                  // <IndividualCLIValue> ::= <InstanceTag>
    @Variablereference_Dollar = 36,            // <VariableReference> ::= '$' <VariableName>
    @Taglist = 37,                             // <TagList> ::= <TagList> <Tag>
    @Taglist2 = 38,                            // <TagList> ::= 
    @Tag = 39,                                 // <Tag> ::= <ObjectInstance>
    @Tag2 = 40,                                // <Tag> ::= <PropertyAssignment>
    @Tag3 = 41,                                // <Tag> ::= <ComponentInstance>
    @Tag4 = 42,                                // <Tag> ::= <VariableTag>
    @Instancetaglist = 43,                     // <InstanceTagList> ::= <InstanceTagList> <InstanceTag>
    @Instancetaglist2 = 44,                    // <InstanceTagList> ::= 
    @Instancetag = 45,                         // <InstanceTag> ::= <ObjectInstance>
    @Instancetag2 = 46,                        // <InstanceTag> ::= <ComponentInstance>
    @Instancetag3 = 47,                        // <InstanceTag> ::= <VariableTag>
    @Objectinstance = 48,                      // <ObjectInstance> ::= <ClosedFormObjectInstance>
    @Objectinstance2 = 49,                     // <ObjectInstance> ::= <OpenFormObjectInstance>
    @Closedformobjectinstance_Lt_Div_Gt = 50,  // <ClosedFormObjectInstance> ::= '<' <ObjectType> <TagAttributeList> '/' '>'
    @Closedformobjectinstance_Lt_Pipe_Div_Gt = 51,  // <ClosedFormObjectInstance> ::= '<' <VariableName> '|' <ObjectType> <TagAttributeList> '/' '>'
    @Openformobjectinstance = 52,              // <OpenFormObjectInstance> ::= <OpeningObjectTag> <TagList> <ClosingObjectTag>
    @Openingobjecttag_Lt_Gt = 53,              // <OpeningObjectTag> ::= '<' <ObjectType> <TagAttributeList> '>'
    @Openingobjecttag_Lt_Pipe_Gt = 54,         // <OpeningObjectTag> ::= '<' <VariableName> '|' <ObjectType> <TagAttributeList> '>'
    @Closingobjecttag_Lt_Div_Gt = 55,          // <ClosingObjectTag> ::= '<' '/' <ObjectType> '>'
    @Closingobjecttag_Lt_Div_Gt2 = 56,         // <ClosingObjectTag> ::= '<' '/' '>'
    @Propertyassignment_Lbracket_Rbracket_Lbracket_Div_Rbracket = 57,  // <PropertyAssignment> ::= '[' <PropertyName> ']' <InstanceTagList> '[' '/' <PropertyName> ']'
    @Propertyassignment_Lbracket_Rbracket_Lbracket_Div_Rbracket2 = 58,  // <PropertyAssignment> ::= '[' <PropertyName> ']' <InstanceTagList> '[' '/' ']'
    @Propertyassignment_Lbracket_Eq_Rbracket = 59,  // <PropertyAssignment> ::= '[' <PropertyName> '=' <SimpleValue> ']'
    @Propertyassignment_Lbracket_Rbracket_Eq = 60,  // <PropertyAssignment> ::= '[' <PropertyName> ']' '=' <InstanceTag>
    @Componentinstance = 61,                   // <ComponentInstance> ::= <ClosedFormComponentInstance>
    @Componentinstance2 = 62,                  // <ComponentInstance> ::= <OpenFormComponentInstance>
    @Closedformcomponentinstance_Lbrace_Div_Rbrace = 63,  // <ClosedFormComponentInstance> ::= '{' <ComponentType> <TagAttributeList> '/' '}'
    @Openformcomponentinstance = 64,           // <OpenFormComponentInstance> ::= <OpeningComponentTag> <TagList> <ClosingComponentTag>
    @Openingcomponenttag_Lbrace_Rbrace = 65,   // <OpeningComponentTag> ::= '{' <ComponentType> <TagAttributeList> '}'
    @Openingcomponenttag_Lbrace_Pipe_Rbrace = 66,  // <OpeningComponentTag> ::= '{' <VariableName> '|' <ComponentType> <TagAttributeList> '}'
    @Closingcomponenttag_Lbrace_Div_Rbrace = 67,  // <ClosingComponentTag> ::= '{' '/' <ComponentType> '}'
    @Closingcomponenttag_Lbrace_Div_Rbrace2 = 68,  // <ClosingComponentTag> ::= '{' '/' '}'
    @Variabletag_Lt_Dollar_Gt = 69,            // <VariableTag> ::= '<' '$' <VariableName> '>'
    @Tagattributelist = 70,                    // <TagAttributeList> ::= <TagAttributeList> <TagAttribute>
    @Tagattributelist2 = 71,                   // <TagAttributeList> ::= 
    @Tagattribute_Eq = 72                      // <TagAttribute> ::= <TagAttributeName> '=' <SimpleValue>
}
