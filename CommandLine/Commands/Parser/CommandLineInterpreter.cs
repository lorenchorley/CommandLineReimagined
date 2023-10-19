using GOLD;
using System;
using CommandLineReimagine.Commands.Parser.SemanticTree;

namespace CommandLineReimagine.Commands.Parser;

public class CommandLineInterpreter
{
    private const string COMPILED_GRAMMAR_EMBEDDED_RESSOURCE = "CommandLineReimagined.Commands.Parser.Grammar.CommandLineGrammar.egt";

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
            case ProductionIndex.Constant_Stringliteral:
                // <Constant> ::= StringLiteral

                if (reduction[0].Data is not string serialisedStringLiteral)
                {
                    result.Errors.Add("Données parsées n'étaient pas de type String");
                    return null;
                }

                return new StringConstant()
                {
                    Value = serialisedStringLiteral
                };

            case ProductionIndex.Id_Identifier:
                // <ID> ::= Identifier
                return new CommandName()
                {
                    Name = (string)reduction[0].Data
                };

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
                throw new NotImplementedException();

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
                // <CommandExpression> ::= <ID> <CommandArgumentList>

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
                // <CommandArgumentList> ::= <CommandArgument>
                return reduction.PassOn();

            case ProductionIndex.Commandargumentlist3:
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
                return reduction.PassOn();

            case ProductionIndex.Value:
                // <Value> ::= <Constant>
                return reduction.PassOn();

            default:
                throw new NotImplementedException();

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
