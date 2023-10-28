using GOLD;
using System;
using Commands.Parser.SemanticTree;

namespace Commands.Parser;

public class CommandLineInterpreter
{
    private const string COMPILED_GRAMMAR_EMBEDDED_RESSOURCE = "Terminal.Commands.Parser.Grammar.CommandLineGrammar.egt";

    private GOLD.Parser _goldParser { get; init; }

    public CommandLineInterpreter()
    {
        _goldParser = GoldEngineParserFactory.BuildParser(COMPILED_GRAMMAR_EMBEDDED_RESSOURCE);
    }

    public ParserResult Parse(string filter)
    {
        ParserResult result = new();

        _goldParser.Open(ref filter);
        _goldParser.TrimReductions = false;

        bool continueParsing = true;
        while (continueParsing && !result.HasErrors)
        {
            ParseMessage response = _goldParser.Parse();
            continueParsing = ProcessResponse(result, response);
        }

        return result;
    }

    private bool ProcessResponse(ParserResult result, ParseMessage response)
    {
        switch (response)
        {
            case ParseMessage.Reduction:
                _goldParser.CurrentReduction = CreateNewObject(result, (Reduction)_goldParser.CurrentReduction);
                break;

            case ParseMessage.Accept:
                // On a fini de parser, on récupère le résultat

                result.Tree = (INode)_goldParser.CurrentReduction;

                return false;

            case ParseMessage.LexicalError:
            case ParseMessage.SyntaxError:
            case ParseMessage.InternalError:
            case ParseMessage.NotLoadedError:
            case ParseMessage.GroupError:
                result.Errors.Add($"{response} à la ligne {_goldParser.CurrentPosition().Line} et à la colonne {_goldParser.CurrentPosition().Column}");

                return false;
        }

        return true; // Continue
    }

    private INode? CreateNewObject(ParserResult result, Reduction r)
    {
        ProductionIndex productionIndex = (ProductionIndex)r.Parent.TableIndex();
        return Interpret(result, r, productionIndex);
    }

    private static INode? Interpret(ParserResult result, Reduction reduction, ProductionIndex productionIndex)
    {
        switch (productionIndex)
        {
            case ProductionIndex.Constant_Stringliteral2:
                // <Constant> ::= 'StringLiteral2'

                if (reduction[0].Data is not string serialisedStringLiteral2)
                {
                    result.Errors.Add("Données parsées n'étaient pas de type String");
                    return null;
                }

                return new StringConstant()
                {
                    Value = serialisedStringLiteral2
                };

            case ProductionIndex.Constant_Stringliteral3:
                // <Constant> ::= 'StringLiteral3'

                if (reduction[0].Data is not string serialisedStringLiteral3)
                {
                    result.Errors.Add("Données parsées n'étaient pas de type String");
                    return null;
                }

                return new StringConstant()
                {
                    Value = serialisedStringLiteral3
                };

            case ProductionIndex.Id_Identifier:
                // <ID> ::= Identifier
                return new CommandName()
                {
                    Name = (string)reduction[0].Data
                };

            case ProductionIndex.Objecttype_Identifier:
                // <ObjectType> ::= Identifier
                break;

            case ProductionIndex.Variablename_Identifier:
                // <VariableName> ::= Identifier
                break;

            case ProductionIndex.Typename_Identifier:
                // <TypeName> ::= Identifier
                break;

            case ProductionIndex.Propertyname_Identifier:
                // <PropertyName> ::= Identifier
                break;

            case ProductionIndex.Attributename_Identifier:
                // <AttributeName> ::= Identifier
                break;

            case ProductionIndex.Componenttype_Identifier:
                // <ComponentType> ::= Identifier
                break;

            case ProductionIndex.Flag_Flagidentifier:
                // <Flag> ::= FlagIdentifier
                return new Flag()
                {
                    Name = (string)reduction[0].Data
                };

            case ProductionIndex.Program:
                // <Program> ::= <PipedCommandList>
                return reduction.PassOn();

            case ProductionIndex.Program2:
                // <Program> ::= 
                return new EmptyCommand();

            case ProductionIndex.Pipedcommandlist_Pipe:
                // <PipedCommandList> ::= <PipedCommandList> '|' <CommandExpression>
                break;

            case ProductionIndex.Pipedcommandlist:
                // <PipedCommandList> ::= <CommandExpression>
                if (reduction.Count() == 0)
                    return new PipedCommandList();
                else if (reduction.Count() == 1)
                    return new PipedCommandList((CommandExpression)reduction[0].Data);
                else
                {
                    var list = (PipedCommandList)reduction[0].Data;
                    list.OrderedCommands.Add((CommandExpression)reduction[1].Data);
                    return list;
                }

            case ProductionIndex.Commandexpression:
                // <CommandExpression> ::= <FunctionExpression>
                break;

            case ProductionIndex.Commandexpression2:
                // <CommandExpression> ::= <CommandExpression_CLINotation>
                break;

            case ProductionIndex.Commandexpression3:
                // <CommandExpression> ::= <IndividualCLIValue>
                break;

            case ProductionIndex.Functionexpression_Lparen_Rparen:
                // <FunctionExpression> ::= <ID> '(' <FunctionArgumentList> ')'
                break;

            case ProductionIndex.Functionexpression_Lparen_Rparen2:
                // <FunctionExpression> ::= <ID> '(' ')'
                break;

            case ProductionIndex.Functionargumentlist_Comma:
                // <FunctionArgumentList> ::= <FunctionArgumentList> ',' <FunctionArgument>
                break;

            case ProductionIndex.Functionargumentlist:
                // <FunctionArgumentList> ::= <FunctionArgument>
                break;

            case ProductionIndex.Functionargument:
                // <FunctionArgument> ::= <RequiredArgument>
                break;

            case ProductionIndex.Functionargument2:
                // <FunctionArgument> ::= <OptionalArgument>
                break;

            case ProductionIndex.Requiredargument:
                // <RequiredArgument> ::= <Value>
                break;

            case ProductionIndex.Optionalargument_Colon:
                // <OptionalArgument> ::= <ID> ':' <Value>
                break;

            case ProductionIndex.Commandexpression_clinotation:
                // <CommandExpression_CLINotation> ::= <ID> <CommandArgumentList>

                return new CommandExpression()
                {
                    Id = (CommandName)reduction[0].Data,
                    Arguments = GetArguments(reduction[1].Data),
                };

            case ProductionIndex.Commandargumentlist:
                // <CommandArgumentList> ::= <CommandArgumentList> <CommandArgument>

                if (reduction.Count() == 0)
                    return new CommandArguments();
                else if (reduction.Count() == 1)
                    return new CommandArguments((CommandArgument)reduction[0].Data);
                else
                {
                    var list = (CommandArguments)GetArguments(reduction[0].Data);
                    list.Arguments.Add((CommandArgument)reduction[1].Data);
                    return list;
                }

            case ProductionIndex.Commandargumentlist2:
                // <CommandArgumentList> ::= 

                if (reduction.Count() == 0)
                    return new CommandArguments();
                else if (reduction.Count() == 1)
                    return new CommandArguments((CommandArgument)reduction[0].Data);
                else
                {
                    var list = (CommandArguments)reduction[0].Data;
                    list.Arguments.Add((CommandArgument)reduction[1].Data);
                    return list;
                }

            case ProductionIndex.Commandargument:
                // <CommandArgument> ::= <Flag>
                return new Flag()
                {
                    Name = (string)reduction[0].Data
                };

            case ProductionIndex.Commandargument2:
                // <CommandArgument> ::= <Value>
                break;

            case ProductionIndex.Value:
                // <Value> ::= <SimpleValue>
                break;

            case ProductionIndex.Value2:
                // <Value> ::= <InstanceTag>
                break;

            case ProductionIndex.Simplevalue:
                // <SimpleValue> ::= <Constant>
                break;

            case ProductionIndex.Simplevalue2:
                // <SimpleValue> ::= <VariableReference>
                break;

            case ProductionIndex.Simplevalue3:
                // <SimpleValue> ::= <ID>
                break;

            case ProductionIndex.Individualclivalue:
                // <IndividualCLIValue> ::= <InstanceTag>
                break;

            case ProductionIndex.Variablereference_Dollar:
                // <VariableReference> ::= '$' <VariableName>
                break;

            case ProductionIndex.Taglist:
                // <TagList> ::= <TagList> <Tag>
                break;

            case ProductionIndex.Taglist2:
                // <TagList> ::= 
                break;

            case ProductionIndex.Tag:
                // <Tag> ::= <ObjectInstance>
                break;

            case ProductionIndex.Tag2:
                // <Tag> ::= <PropertyAssignment>
                break;

            case ProductionIndex.Tag3:
                // <Tag> ::= <ComponentInstance>
                break;

            case ProductionIndex.Tag4:
                // <Tag> ::= <VariableTag>
                break;

            case ProductionIndex.Instancetaglist:
                // <InstanceTagList> ::= <InstanceTagList> <InstanceTag>
                break;

            case ProductionIndex.Instancetaglist2:
                // <InstanceTagList> ::= 
                break;

            case ProductionIndex.Instancetag:
                // <InstanceTag> ::= <ObjectInstance>
                break;

            case ProductionIndex.Instancetag2:
                // <InstanceTag> ::= <ComponentInstance>
                break;

            case ProductionIndex.Instancetag3:
                // <InstanceTag> ::= <VariableTag>
                break;

            case ProductionIndex.Objectinstance:
                // <ObjectInstance> ::= <ClosedFormObjectInstance>
                break;

            case ProductionIndex.Objectinstance2:
                // <ObjectInstance> ::= <OpenFormObjectInstance>
                break;

            case ProductionIndex.Closedformobjectinstance_Lt_Div_Gt:
                // <ClosedFormObjectInstance> ::= '<' <ObjectType> <AttributeList> '/' '>'
                break;

            case ProductionIndex.Openformobjectinstance:
                // <OpenFormObjectInstance> ::= <OpeningObjectTag> <TagList> <ClosingObjectTag>
                break;

            case ProductionIndex.Openingobjecttag_Lt_Gt:
                // <OpeningObjectTag> ::= '<' <ObjectType> <AttributeList> '>'
                break;

            case ProductionIndex.Openingobjecttag_Lt_Pipe_Gt:
                // <OpeningObjectTag> ::= '<' <VariableName> '|' <ObjectType> <AttributeList> '>'
                break;

            case ProductionIndex.Closingobjecttag_Lt_Div_Gt:
                // <ClosingObjectTag> ::= '<' '/' <ObjectType> '>'
                break;

            case ProductionIndex.Closingobjecttag_Lt_Div_Gt2:
                // <ClosingObjectTag> ::= '<' '/' '>'
                break;

            case ProductionIndex.Propertyassignment_Lbracket_Rbracket_Lbracket_Div_Rbracket:
                // <PropertyAssignment> ::= '[' <PropertyName> ']' <InstanceTagList> '[' '/' <PropertyName> ']'
                break;

            case ProductionIndex.Propertyassignment_Lbracket_Rbracket_Lbracket_Div_Rbracket2:
                // <PropertyAssignment> ::= '[' <PropertyName> ']' <InstanceTagList> '[' '/' ']'
                break;

            case ProductionIndex.Propertyassignment_Lbracket_Eq_Rbracket:
                // <PropertyAssignment> ::= '[' <PropertyName> '=' <SimpleValue> ']'
                break;

            case ProductionIndex.Propertyassignment_Lbracket_Rbracket_Eq:
                // <PropertyAssignment> ::= '[' <PropertyName> ']' '=' <InstanceTag>
                break;

            case ProductionIndex.Componentinstance:
                // <ComponentInstance> ::= <ClosedFormComponentInstance>
                break;

            case ProductionIndex.Componentinstance2:
                // <ComponentInstance> ::= <OpenFormComponentInstance>
                break;

            case ProductionIndex.Closedformcomponentinstance_Lbrace_Div_Rbrace:
                // <ClosedFormComponentInstance> ::= '{' <ComponentType> <AttributeList> '/' '}'
                break;

            case ProductionIndex.Openformcomponentinstance:
                // <OpenFormComponentInstance> ::= <OpeningComponentTag> <TagList> <ClosingComponentTag>
                break;

            case ProductionIndex.Openingcomponenttag_Lbrace_Rbrace:
                // <OpeningComponentTag> ::= '{' <ComponentType> <AttributeList> '}'
                break;

            case ProductionIndex.Openingcomponenttag_Lbrace_Pipe_Rbrace:
                // <OpeningComponentTag> ::= '{' <VariableName> '|' <ComponentType> <AttributeList> '}'
                break;

            case ProductionIndex.Closingcomponenttag_Lbrace_Div_Rbrace:
                // <ClosingComponentTag> ::= '{' '/' <ComponentType> '}'
                break;

            case ProductionIndex.Closingcomponenttag_Lbrace_Div_Rbrace2:
                // <ClosingComponentTag> ::= '{' '/' '}'
                break;

            case ProductionIndex.Variabletag_Lt_Dollar_Gt:
                // <VariableTag> ::= '<' '$' <VariableName> '>'
                break;

            case ProductionIndex.Attributelist:
                // <AttributeList> ::= <AttributeList> <Attribute>
                break;

            case ProductionIndex.Attributelist2:
                // <AttributeList> ::= 
                break;

            case ProductionIndex.Attribute_Eq:
                // <Attribute> ::= <AttributeName> '=' <SimpleValue>
                break;

        }

        throw new NotImplementedException();
    }

    private static CommandArguments GetArguments(object obj)
    {
        if (obj is CommandArguments args)
        {
            return args;
        }
        else if (obj is CommandArgument arg)
        {
            return new CommandArguments(arg);
        }

        throw new Exception("Unknown argument type");
    }
}
