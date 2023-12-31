﻿using Commands.Parser;
using Commands.Parser.SemanticTree;
using Controller;
using EntityComponentSystem;
using GOLD;
using OneOf;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Terminal.Commands.Parser.Serialisation;
using Terminal.Search;
using UIComponents.Components;

namespace Terminal;

public class Prompt : IECSSubsystem
{
    private readonly ECS _ecs;
    private readonly LoopController _loopController;
    private readonly CommandSearch _commandSearch;
    private readonly Scene _scene;
    private string _text = "";
    private RootNode? _parsedCommand = null;
    private CommandLineInterpreter _commandLineInterpreter = new();

    private Entity _inputPromptEntity;

    private int _cursorPosition;

    public Prompt(ECS ecs, LoopController loopController, CommandSearch commandSearch, Scene scene)
    {
        _ecs = ecs;
        _loopController = loopController;
        _commandSearch = commandSearch;
        _scene = scene;
    }

    public void OnInit()
    {
    }

    public void OnStart()
    {
        InitPromptText();
    }

    public void InitPromptText()
    {
        RefreshText();
        _loopController.RequestLoop();
    }

    public void SetPromptText(string text)
    {
        if (!string.Equals(_text, text, StringComparison.Ordinal))
        {
            Debug.WriteLine(text);

            _text = text;
            RefreshText();
            _loopController.RequestLoop();

        }
    }

    public bool TryGetValidCommand([NotNullWhen(true)] out RootNode? parsedCommand, [NotNullWhen(true)] out string? commandText)
    {
        parsedCommand = _parsedCommand;
        commandText = _text;
        return _parsedCommand != null;
    }

    public void SetCursorPosition(int cursorPosition)
    {
        _cursorPosition = cursorPosition;
        _scene.Cursor.Position = _cursorPosition;
        _loopController.RequestLoop();
    }

    private void RefreshText()
    {
        // Cleanup ?
        //_scene.InputPanel.Lines.Clear();
        _inputPromptEntity?.RemoveAllChildren();

        LineComponent currentLine = AddNewLineToPrompt();

        // Ajouter et configurer le cursor pour le texte actuel
        currentLine.AddLineSegment(_scene.Cursor);
        _scene.Cursor.Position = _cursorPosition;
        _scene.Cursor.Text = _text;

        ParserResult<RootNode> result = _commandLineInterpreter.Parse<RootNode>(_text);

        _parsedCommand = null;

        result.Switch(
            tree =>
            {
                _parsedCommand = tree;

                var visitor = new UITokenisationVisitor(_scene.InputPanel.Entity);
                tree.Accept(visitor);

                _scene.Cursor.TextComponentReference =
                    _scene.InputPanel
                          .Lines
                          .SelectMany(line => line.LineSegments)
                          .OfType<TextComponent>()
                          .First();

                // Type check here or in Shell ?

                // Si le texte termine avec un identifiant, on affiche les suggestions de complétion
                ShowIdentifierSuggestions(_text, currentLine);

                _scene.IsCommandExecutable = true;
            },
            parserError =>
            {
                _scene.IsCommandExecutable = false;

                parserError.Switch(

                    errors =>
                    {
                        // Ajouter les autres messages d'erreur au dessus du prompt
                        if (errors.Count > 0)
                        {
                            currentLine.LinkNewTextBlock("Error messages", errors.Join(' '));

                            currentLine = AddNewLineToPrompt();
                        }

                        currentLine.LinkNewTextBlock("Text with parsing error", _text);
                    },
                    syntaxError =>
                    {
                        var textLines = _text.Replace("\r", "").Split('\n');
                        var line = textLines[syntaxError.Line];

                        // Si l'erreur se position à la fin de la ligne
                        if (syntaxError.Column == line.Length)
                            HandleEndOfLineSyntaxError(syntaxError, currentLine);
                        else
                            HandleMidLineSyntaxError(syntaxError, currentLine);
                    },
                    lexicalError => HandleLexicalError(lexicalError, currentLine));
            }
        );

    }

    private void ShowIdentifierSuggestions(OneOf<RootNode, string> input, LineComponent currentLine)
    {
        string? word =
            input.Match(
                rootNode => (GetIdentifierAtEndOfLine(rootNode)?.Name),
                text =>
                {
                    // S'il n'y a pas de texte, on fait rien
                    if (text.Trim().Length == 0)
                        return null;

                    // Sans le dernier mot, est-ce que identifier est une option ?
                    int lastSpaceIndex = text.LastIndexOf(' ');

                    //TODO Parser that checks for just a valid identifier

                    if (lastSpaceIndex == -1)
                    {
                        if (_commandLineInterpreter.IsValidIdentifier(text))
                            return text;
                        else
                            return null;
                    }

                    var textWithoutLastWord = text.Substring(0, lastSpaceIndex);

                    var syntaxError = _commandLineInterpreter.HasSyntaxError(textWithoutLastWord);

                    if (syntaxError == null)
                        return null;

                    var symbols = ListSymbols(syntaxError);
                    if (!symbols.Contains("command"))
                        return null;

                    return text.Split(' ').LastOrDefault();
                }
            );

        if (string.IsNullOrWhiteSpace(word))
            return;

        var suggestions = _commandSearch.GetSuggestions(word).ToList();

        if (suggestions != null)
        {
            currentLine.LinkNewTextBlock("Completion suggestions", " ");
            foreach (var suggestion in suggestions)
            {
                currentLine.LinkNewTextBlock("Completion suggestion", suggestion, highlighted: true);
                currentLine.LinkNewTextBlock("Completion suggestion", " ");
            }
        }
    }

    private Identifier? GetIdentifierAtEndOfLine(RootNode tree)
    {
        return null;
    }

    private void HandleLexicalError(LexicalError lexicalError, LineComponent currentLine)
    {
        FillLineWithSyntaxGuides(lexicalError.SyntaxError, currentLine);

        LineComponent newLine = AddNewLineToPrompt();

        var textInError = newLine.LinkNewTextBlock("Text with parsing error", _text);
        newLine.LinkNewTextHighlight(textInError, lexicalError.SyntaxError.Line, lexicalError.SyntaxError.Column);
    }

    private void HandleMidLineSyntaxError(SyntaxError syntaxError, LineComponent currentLine)
    {
        FillLineWithSyntaxGuides(syntaxError, currentLine);

        LineComponent newLine = AddNewLineToPrompt();
        var textInError = newLine.LinkNewTextBlock("Text with parsing error", _text);
        newLine.LinkNewTextHighlight(textInError, syntaxError.Line, syntaxError.Column);
    }

    private void HandleEndOfLineSyntaxError(SyntaxError syntaxError, LineComponent currentLine)
    {
        ShowIdentifierSuggestions(_text, currentLine);

        FillLineWithSyntaxGuides(syntaxError, currentLine);

        LineComponent newLine = AddNewLineToPrompt();
        var textInError = newLine.LinkNewTextBlock("Text with parsing error", _text);
    }

    private static void FillLineWithSyntaxGuides(SyntaxError syntaxError, LineComponent currentLine)
    {
        currentLine.LinkNewTextBlock("Error messages", $"Expecting ");
        foreach (var item in SerialiseSymbols(syntaxError))
        {
            currentLine.LinkNewTextBlock("Completion suggestion", item, highlighted: true);
            currentLine.LinkNewTextBlock("Completion suggestion", " ");
        }
    }

    private static Dictionary<string, string[]> _symbolTranslations = new()
    {
        { "Identifier", new string[] { "command", "variable", "type" } },
        { "FlagIdentifier", new string[] { "-" } },
        { "'StringLiteral2'", new string[] { "\"" } },
        { "'StringLiteral3'", new string[] { "\"" } },
        { "(EOF)", new string[] { "End of input" } },
    };


    private static IEnumerable<string> SerialiseSymbols(SyntaxError syntaxError)
        => ListSymbols(syntaxError)
            .Distinct();

    private static IEnumerable<string> ListSymbols(SyntaxError syntaxError)
    {
        for (int i = 0; i < syntaxError.ExpectedSymbols.Count(); i++)
        {
            Symbol s = syntaxError.ExpectedSymbols[i];
            string text = s.Text();

            if (_symbolTranslations.TryGetValue(text, out string[]? translations))
            {
                foreach (var translation in translations)
                {
                    yield return translation;
                }

                continue;
            }

            yield return text.Trim('\'');
        }
    }

    private LineComponent AddNewLineToPrompt()
    {
        _inputPromptEntity ??= _ecs.NewEntity("Input prompt");
        Entity newChild = _inputPromptEntity.NewChildEntity("Prompt line");
        LineComponent newLine = newChild.AddComponent<LineComponent>();
        //_scene.InputPanel.Lines.Add(newLine); // Keep or rely on children entites ?
        return newLine;
    }

}
