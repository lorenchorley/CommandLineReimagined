using Commands.Parser.SemanticTree;
using GOLD;
using OneOf;
using OneOf.Types;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Schema;
using ValueOf;

namespace Commands.Parser;

[GenerateOneOf]
public partial class ParserError : OneOfBase<List<string>, SyntaxError, LexicalError>
{
}
public class SyntaxError
{
    public int Line { get; init; }
    public int Column { get; init; }
    public SymbolList ExpectedSymbols { get; init; }
}

public class LexicalError
{
    public SyntaxError SyntaxError { get; init; }
}

public class CommandLineInterpreter
{
    private const string NameSpace = "Terminal";
    private const string RelativePath = "Commands.Parser.Grammar";
    private const string FullGrammarGrammarFile = "CommandLineGrammar.egt";
    private const string IsValidIdentifierGrammarFile = "IsValidIdentifier.egt";

    private GOLD.Parser _fullGrammarParser { get; init; }
    private GOLD.Parser _isValidIdentifierParser { get; init; }

    public CommandLineInterpreter()
    {
        _fullGrammarParser = GoldEngineParserFactory.BuildParser($"{NameSpace}.{RelativePath}.{FullGrammarGrammarFile}");
        _isValidIdentifierParser = GoldEngineParserFactory.BuildParser($"{NameSpace}.{RelativePath}.{IsValidIdentifierGrammarFile}");
    }

    public bool IsValidIdentifier(string text)
    {
        ParserResult<Identifier> result = Parse<Identifier>(text, _isValidIdentifierParser);

        return result.IsT0;
    }

    public SyntaxError? HasSyntaxError(string text)
    {
        ParserResult<RootNode> result = Parse<RootNode>(text);

        if (!result.IsT1)
            return null;

        var parserError = result.AsT1;
        var syntaxError =
            parserError.Match<SyntaxError?>(
                errors => null,
                syntaxError => syntaxError,
                lexicalError => lexicalError.SyntaxError
            );

        return syntaxError;
    }

    public ParserResult<TRoot> Parse<TRoot>(string filter, GOLD.Parser? parser = null)
    {
        parser ??= _fullGrammarParser;

        List<string> errors = new();
        TRoot? treeRoot = default;

        parser.Open(ref filter);
        parser.TrimReductions = false;

        bool continueParsing = true;
        while (continueParsing && errors.Count == 0)
        {
            ParseMessage response = parser.Parse();
            switch (response)
            {
                case ParseMessage.Reduction:
                    parser.CurrentReduction = CreateNewObject(errors, (Reduction)parser.CurrentReduction);
                    break;

                case ParseMessage.Accept:
                    // On a fini de parser, on récupère le résultat

                    treeRoot = (TRoot)parser.CurrentReduction;

                    continueParsing = false;
                    break;

                case ParseMessage.SyntaxError:
                    var syntaxError = new SyntaxError()
                    {
                        Line = parser.CurrentPosition().Line,
                        Column = parser.CurrentPosition().Column,
                        ExpectedSymbols = parser.ExpectedSymbols()
                    };

                    // TODO Si lexicalerror, enleve un charactère et réessaye ?
                    // Possibilité de revenir sur un état précédent pour donner plus d'indice sur quoi il faut faire ?

                    return new ParserResult<TRoot>(new ParserError(syntaxError));

                case ParseMessage.LexicalError:

                    var lexicalError = new LexicalError()
                    {
                        SyntaxError = new SyntaxError()
                        {
                            Line = parser.CurrentPosition().Line,
                            Column = parser.CurrentPosition().Column,
                            ExpectedSymbols = parser.ExpectedSymbols()
                        }
                    };

                    // TODO Si lexicalerror, enleve un charactère et réessaye ?
                    // Possibilité de revenir sur un état précédent pour donner plus d'indice sur quoi il faut faire ?

                    return new ParserResult<TRoot>(new ParserError(lexicalError));

                case ParseMessage.InternalError:
                case ParseMessage.NotLoadedError:
                case ParseMessage.GroupError:

                    errors.Add($"Erreur de parsing : {response}");
                    break;
            }
        }

        if (errors.Count > 0)
        {
            return new ParserResult<TRoot>(new ParserError(errors));
        }

        return new ParserResult<TRoot>(treeRoot);
    }

    private object CreateNewObject(List<string> errors, Reduction r)
    {
        ProductionIndex productionIndex = (ProductionIndex)r.Parent.TableIndex();
        return Interpret(errors, r, productionIndex);
    }

    private static object Interpret(List<string> errors, Reduction reduction, ProductionIndex productionIndex)
    {
        switch (productionIndex)
        {
            case ProductionIndex.Constant_Stringliteral2:
                // <Constant> ::= 'StringLiteral2'

                if (reduction[0].Data is not string serialisedStringLiteral2)
                {
                    errors.Add("Données parsées n'étaient pas de type String");
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
                    errors.Add("Données parsées n'étaient pas de type String");
                    return null;
                }

                return new StringConstant()
                {
                    Value = serialisedStringLiteral3
                };

            case ProductionIndex.Id_Identifier:
                // <ID> ::= Identifier
                return new Identifier()
                {
                    Name = (string)reduction[0].Data
                };

            case ProductionIndex.Objecttype_Identifier:
                // <ObjectType> ::= Identifier
                return new ObjectType() { Value = (string)reduction[0].Data };

            case ProductionIndex.Variablename_Identifier:
                // <VariableName> ::= Identifier
                return new VariableName() { Name = (string)reduction[0].Data };

            case ProductionIndex.Typename_Identifier:
                // <TypeName> ::= Identifier
                throw new NotImplementedException();

            case ProductionIndex.Propertyname_Identifier:
                // <PropertyName> ::= Identifier
                return new ProperyName()
                {
                    Name = (string)reduction[0].Data
                };

            case ProductionIndex.Tagattributename_Identifier:
                // <AttributeName> ::= Identifier
                return new TagAttributeName()
                {
                    Name = (string)reduction[0].Data
                };

            case ProductionIndex.Componenttype_Identifier:
                // <ComponentType> ::= Identifier
                throw new NotImplementedException();

            case ProductionIndex.Flag_Flagidentifier:
                // <Flag> ::= FlagIdentifier
                return new CommandArgumentFlag()
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
                if (reduction.Count() == 0)
                    return new PipedCommandList();
                else if (reduction.Count() == 1)
                    return new PipedCommandList(ConvertToCommandExpression(reduction[0].Data));
                else
                {
                    var list = (PipedCommandList)reduction[0].Data;
                    list.OrderedCommands.Add(GetCommandExpression(reduction[2].Data));
                    return list;
                }

            case ProductionIndex.Pipedcommandlist:
                // <PipedCommandList> ::= <CommandExpression>
                if (reduction.Count() == 0)
                    return new PipedCommandList();
                else if (reduction.Count() == 1)
                    return new PipedCommandList(ConvertToCommandExpression(reduction[0].Data));
                else
                {
                    var list = (PipedCommandList)reduction[0].Data;
                    list.OrderedCommands.Add(GetCommandExpression(reduction[1].Data));
                    return list;
                }

            case ProductionIndex.Commandexpression:
                // <CommandExpression> ::= <FunctionExpression>
                return reduction.PassOn();

            case ProductionIndex.Commandexpression2:
                // <CommandExpression> ::= <CommandExpression_CLINotation>
                return reduction.PassOn();

            case ProductionIndex.Commandexpression3:
                // <CommandExpression> ::= <IndividualCLIValue>
                return reduction.PassOn();

            case ProductionIndex.Functionexpression_Lparen_Rparen:
                // <FunctionExpression> ::= <ID> '(' <FunctionArgumentList> ')'
                return new FunctionExpression()
                {
                    Id = (Identifier)reduction[0].Data,
                    Arguments = (CommandArguments)reduction[2].Data
                };

            case ProductionIndex.Functionexpression_Lparen_Rparen2:
                // <FunctionExpression> ::= <ID> '(' ')'
                return new FunctionExpression()
                {
                    Id = (Identifier)reduction[0].Data,
                    Arguments = new CommandArguments()
                };

            case ProductionIndex.Functionargumentlist_Comma:
                // <FunctionArgumentList> ::= <FunctionArgumentList> ',' <FunctionArgument>
                throw new NotImplementedException();

            case ProductionIndex.Functionargumentlist:
                // <FunctionArgumentList> ::= <FunctionArgument>
                return new CommandArguments((CommandArgument)reduction[0].Data);

            case ProductionIndex.Functionargument:
                // <FunctionArgument> ::= <RequiredArgument>
                return reduction.PassOn();

            case ProductionIndex.Functionargument2:
                // <FunctionArgument> ::= <OptionalArgument>
                return reduction.PassOn();

            case ProductionIndex.Requiredargument:
                // <RequiredArgument> ::= <Value>
                return new RequiredCommandArgument()
                {
                    Value = (Value)reduction[0].Data
                };

            case ProductionIndex.Optionalargument_Colon:
                // <OptionalArgument> ::= <ID> ':' <Value>
                return new OptionalCommandArgument()
                {
                    Name = (Identifier)reduction[0].Data,
                    Value = (Value)reduction[2].Data
                };

            case ProductionIndex.Commandexpression_clinotation:
                // <CommandExpression_CLINotation> ::= <ID> <CommandArgumentList>

                if (reduction[0].Data is Identifier id)
                {
                    return new CommandExpressionCli()
                    {
                        Name = new CommandName() { Name = id.Name },
                        Arguments = GetArguments(reduction[1].Data),
                    };
                }
                else if (reduction[0].Data is CommandName commandName)
                {
                    return new CommandExpressionCli()
                    {
                        Name = commandName,
                        Arguments = GetArguments(reduction[1].Data),
                    };
                }

                throw new NotImplementedException();

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
                return reduction.PassOn();
            //return new CommandArgumentFlag()
            //{
            //    Name = (string)reduction[0].Data
            //};

            case ProductionIndex.Commandargument2:
                // <CommandArgument> ::= <Value>
                return new CommandArgumentValue()
                {
                    Value = (Value)reduction[0].Data
                };

            case ProductionIndex.Value:
                // <Value> ::= <SimpleValue>
                return reduction.PassOn();

            case ProductionIndex.Value2:
                // <Value> ::= <InstanceTag>
                throw new NotImplementedException();

            case ProductionIndex.Simplevalue:
                // <SimpleValue> ::= <Constant>
                return reduction.PassOn();

            case ProductionIndex.Simplevalue2:
                // <SimpleValue> ::= <VariableReference>
                return reduction.PassOn();

            case ProductionIndex.Simplevalue3:
                // <SimpleValue> ::= <ID>
                return reduction.PassOn();

            case ProductionIndex.Individualclivalue:
                // <IndividualCLIValue> ::= <InstanceTag>
                return reduction.PassOn();

            case ProductionIndex.Variablereference_Dollar:
                // <VariableReference> ::= '$' <VariableName>
                return new VariableReference()
                {
                    Name = (VariableName)reduction[1].Data
                };

            case ProductionIndex.Taglist:
                // <TagList> ::= <TagList> <Tag>

                if (reduction.Count() == 0)
                    return new TagList();
                else if (reduction.Count() == 1)
                    return new TagList((Tag)reduction[0].Data);
                else
                {
                    var list = (TagList)reduction[0].Data;
                    list.Tags.Add((Tag)reduction[1].Data);
                    return list;
                }


            case ProductionIndex.Taglist2:
                // <TagList> ::= 

                if (reduction.Count() == 0)
                    return new TagList();
                else if (reduction.Count() == 1)
                    return new TagList((Tag)reduction[0].Data);
                else
                {
                    var list = (TagList)reduction[0].Data;
                    list.Tags.Add((Tag)reduction[1].Data);
                    return list;
                }


            case ProductionIndex.Tag:
                // <Tag> ::= <ObjectInstance>
                return reduction.PassOn();

            case ProductionIndex.Tag2:
                // <Tag> ::= <PropertyAssignment>
                return reduction.PassOn();

            case ProductionIndex.Tag3:
                // <Tag> ::= <ComponentInstance>
                throw new NotImplementedException();

            case ProductionIndex.Tag4:
                // <Tag> ::= <VariableTag>
                return reduction.PassOn();

            case ProductionIndex.Instancetaglist:
                // <InstanceTagList> ::= <InstanceTagList> <InstanceTag>
                throw new NotImplementedException();

            case ProductionIndex.Instancetaglist2:
                // <InstanceTagList> ::= 
                throw new NotImplementedException();

            case ProductionIndex.Instancetag:
                // <InstanceTag> ::= <ObjectInstance>
                return reduction.PassOn();

            case ProductionIndex.Instancetag2:
                // <InstanceTag> ::= <ComponentInstance>
                throw new NotImplementedException();

            case ProductionIndex.Instancetag3:
                // <InstanceTag> ::= <VariableTag>
                return reduction.PassOn();

            case ProductionIndex.Objectinstance:
                // <ObjectInstance> ::= <ClosedFormObjectInstance>
                return reduction.PassOn();

            case ProductionIndex.Objectinstance2:
                // <ObjectInstance> ::= <OpenFormObjectInstance>
                return reduction.PassOn();

            case ProductionIndex.Closedformobjectinstance_Lt_Div_Gt:
                // <ClosedFormObjectInstance> ::= '<' <ObjectType> <AttributeList> '/' '>'
                return new ObjectInstance()
                {
                    ObjectType = (ObjectType)reduction[1].Data,
                    Attributes = (TagAttributeList)reduction[2].Data,
                    Children = null,
                    VariableName = null
                };

            case ProductionIndex.Closedformobjectinstance_Lt_Pipe_Div_Gt:
                // <ClosedFormObjectInstance> ::= '<' <VariableName> '|' <ObjectType> <TagAttributeList> '/' '>'
                throw new NotImplementedException();

            case ProductionIndex.Openformobjectinstance:
                // <OpenFormObjectInstance> ::= <OpeningObjectTag> <TagList> <ClosingObjectTag>
                Tag tag = (Tag)reduction[0].Data;
                TagList children = (TagList)reduction[1].Data;
                ClosingTag closingTag = (ClosingTag)reduction[2].Data;

                if (tag is ObjectInstance obj)
                {
                    if (closingTag.TagObjectType != null && !string.Equals(closingTag.TagObjectType.Value, obj.ObjectType.Value, StringComparison.Ordinal))
                    {
                        errors.Add($"Le tag de fermeture {closingTag.TagObjectType.Value} ne correspond pas au tag d'ouverture {obj.ObjectType.Value}");
                    }

                    obj.Children = children;
                    return tag;
                }

                throw new NotImplementedException();

            case ProductionIndex.Openingobjecttag_Lt_Gt:
                // <OpeningObjectTag> ::= '<' <ObjectType> <AttributeList> '>'
                return new ObjectInstance()
                {
                    ObjectType = (ObjectType)reduction[1].Data,
                    Attributes = (TagAttributeList)reduction[2].Data,
                    Children = null,
                    VariableName = null
                };

            case ProductionIndex.Openingobjecttag_Lt_Pipe_Gt:
                // <OpeningObjectTag> ::= '<' <VariableName> '|' <ObjectType> <AttributeList> '>'
                return new ObjectInstance()
                {
                    VariableName = (VariableName)reduction[1].Data,
                    ObjectType = (ObjectType)reduction[3].Data,
                    Attributes = (TagAttributeList)reduction[4].Data
                };

            case ProductionIndex.Closingobjecttag_Lt_Div_Gt:
                // <ClosingObjectTag> ::= '<' '/' <ObjectType> '>'
                return new ClosingTag()
                {
                    TagObjectType = (ObjectType)reduction[2].Data
                };

            case ProductionIndex.Closingobjecttag_Lt_Div_Gt2:
                // <ClosingObjectTag> ::= '<' '/' '>'
                return new ClosingTag();

            case ProductionIndex.Propertyassignment_Lbracket_Rbracket_Lbracket_Div_Rbracket:
                // <PropertyAssignment> ::= '[' <PropertyName> ']' <InstanceTagList> '[' '/' <PropertyName> ']'
                throw new NotImplementedException();

            case ProductionIndex.Propertyassignment_Lbracket_Rbracket_Lbracket_Div_Rbracket2:
                // <PropertyAssignment> ::= '[' <PropertyName> ']' <InstanceTagList> '[' '/' ']'
                throw new NotImplementedException();

            case ProductionIndex.Propertyassignment_Lbracket_Eq_Rbracket:
                // <PropertyAssignment> ::= '[' <PropertyName> '=' <SimpleValue> ']'
                return new PropertyAssignment()
                {
                    Name = (ProperyName)reduction[1].Data,
                    Value = (SimpleValue)reduction[3].Data
                };

            case ProductionIndex.Propertyassignment_Lbracket_Rbracket_Eq:
                // <PropertyAssignment> ::= '[' <PropertyName> ']' '=' <InstanceTag>
                throw new NotImplementedException();

            case ProductionIndex.Componentinstance:
                // <ComponentInstance> ::= <ClosedFormComponentInstance>
                throw new NotImplementedException();

            case ProductionIndex.Componentinstance2:
                // <ComponentInstance> ::= <OpenFormComponentInstance>
                throw new NotImplementedException();

            case ProductionIndex.Closedformcomponentinstance_Lbrace_Div_Rbrace:
                // <ClosedFormComponentInstance> ::= '{' <ComponentType> <AttributeList> '/' '}'
                throw new NotImplementedException();

            case ProductionIndex.Openformcomponentinstance:
                // <OpenFormComponentInstance> ::= <OpeningComponentTag> <TagList> <ClosingComponentTag>
                throw new NotImplementedException();

            case ProductionIndex.Openingcomponenttag_Lbrace_Rbrace:
                // <OpeningComponentTag> ::= '{' <ComponentType> <AttributeList> '}'
                throw new NotImplementedException();

            case ProductionIndex.Openingcomponenttag_Lbrace_Pipe_Rbrace:
                // <OpeningComponentTag> ::= '{' <VariableName> '|' <ComponentType> <AttributeList> '}'
                throw new NotImplementedException();

            case ProductionIndex.Closingcomponenttag_Lbrace_Div_Rbrace:
                // <ClosingComponentTag> ::= '{' '/' <ComponentType> '}'
                throw new NotImplementedException();

            case ProductionIndex.Closingcomponenttag_Lbrace_Div_Rbrace2:
                // <ClosingComponentTag> ::= '{' '/' '}'
                throw new NotImplementedException();

            case ProductionIndex.Variabletag_Lt_Dollar_Gt:
                // <VariableTag> ::= '<' '$' <VariableName> '>'
                return new VariableTag()
                {
                    Name = (VariableName)reduction[2].Data
                };

            case ProductionIndex.Tagattributelist:
                // <TagAttributeList> ::= <TagAttributeList> <TagAttribute>

                if (reduction.Count() == 0)
                    return new TagAttributeList();
                else if (reduction.Count() == 1)
                    return new TagAttributeList((TagAttribute)reduction[0].Data);
                else
                {
                    var list = (TagAttributeList)reduction[0].Data;
                    list.Attributes.Add((TagAttribute)reduction[1].Data);
                    return list;
                }

            case ProductionIndex.Tagattributelist2:
                // <TagAttributeList> ::= 

                if (reduction.Count() == 0)
                    return new TagAttributeList();
                else if (reduction.Count() == 1)
                    return new TagAttributeList((TagAttribute)reduction[0].Data);
                else
                {
                    var list = (TagAttributeList)reduction[0].Data;
                    list.Attributes.Add((TagAttribute)reduction[1].Data);
                    return list;
                }

            case ProductionIndex.Tagattribute_Eq:
                // <TagAttribute> ::= <TagAttributeName> '=' <SimpleValue>
                return new TagAttribute()
                {
                    Name = (TagAttributeName)reduction[0].Data,
                    Value = (SimpleValue)reduction[2].Data
                };

        }

        throw new NotImplementedException();
    }

    private static CommandExpression GetCommandExpression(object value)
    {
        if (value is CommandExpression cmd)
        {
            return cmd;
        }
        else if (value is FunctionExpression fe)
        {
            return new CommandExpression()
            {
                Expression = fe
            };
        }
        else if (value is CommandExpressionCli cli)
        {
            return new CommandExpression()
            {
                Expression = cli
            };
        } 
        else if (value is ObjectInstance obj)
        {
            return new CommandExpression()
            {
                Expression = obj
            };
        }

        throw new Exception("Unknown expression type");
    }

    private static CommandExpression ConvertToCommandExpression(object value)
    {
        if (value is ObjectInstance obj)
        {
            return new CommandExpression()
            {
                Expression = obj
            };
        }
        else if (value is CommandExpressionCli cli)
        {
            return new CommandExpression()
            {
                Expression = cli
            };
        }
        else if (value is FunctionExpression functionExpression)
        {
            return new CommandExpression()
            {
                Expression = functionExpression
            };
        }

        throw new Exception("Unknown expression type");
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

