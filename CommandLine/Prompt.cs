using CommandLine.Modules;
using Commands.Parser;
using Commands.Parser.SemanticTree;
using UIComponents;
using UIComponents.Components;
using EntityComponentSystem;
using GOLD;
using OneOf;
using Rendering;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Terminal.Commands.Parser.Serialisation;
using Terminal.Search;
using UIComponents.Compoents.Console;

namespace Terminal;

public class Prompt
{
    private readonly ECS _ecs;
    private readonly PathModule _pathModule;
    private readonly RenderLoop _renderLoop;
    private readonly CommandSearch _commandSearch;

    private string _text = "";
    private RootNode? _parsedCommand = null;
    private CommandLineInterpreter _commandLineInterpreter = new();

    private int _cursorPosition;
    private CursorComponent cursor;

    public ConsoleInputPanel Input { get; set; }
    public ConsoleOutputPanel Output { get; set; }

    public Prompt(ECS ecs, PathModule pathModule, RenderLoop renderLoop, CommandSearch commandSearch)
    {
        _ecs = ecs;
        _pathModule = pathModule;
        _renderLoop = renderLoop;
        _commandSearch = commandSearch;
        Entity cursorEntity = _ecs.NewEntity("Cursor");
        cursor = cursorEntity.AddComponent<CursorComponent>();
    }

    public void InitPromptText()
    {
        RefreshText();
        _renderLoop.RefreshOnce();
    }

    public void SetPromptText(string text)
    {
        if (!string.Equals(_text, text, StringComparison.Ordinal))
        {
            Debug.WriteLine(text);

            _text = text;
            RefreshText();
            _renderLoop.RefreshOnce();

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
        cursor.Position = _cursorPosition;
        _renderLoop.RefreshOnce();
    }

    private void RefreshText()
    {
        // Cleanup ?
        Input.PromptLines.Clear();

        Entity entity = _ecs.NewEntity("Input prompt and command");
        LineComponent currentLine = entity.AddComponent<LineComponent>();
        Input.PromptLines.Add(currentLine);

        // Ajouter et configurer le cursor pour le texte actuel
        currentLine.AddLineSegment(cursor);
        cursor.Position = _cursorPosition;
        cursor.Text = _text;

        ParserResult<RootNode> result = _commandLineInterpreter.Parse<RootNode>(_text);

        _parsedCommand = null;

        result.Switch(
            tree =>
            {
                _parsedCommand = tree;

                var visitor = new UITokenisationVisitor(Input.PromptLines);
                tree.Accept(visitor);

                cursor.TextComponentReference =
                    Input
                          .PromptLines
                          .SelectMany(line => line.LineSegments)
                          .OfType<TextComponent>()
                          .First();

                // Type check here or in Shell ?

                // Si le texte termine avec un identifiant, on affiche les suggestions de complétion
                ShowIdentifierSuggestions(_text, currentLine);

                Input.IsCommandExecutable = true;
            },
            parserError =>
            {
                Input.IsCommandExecutable = false;

                parserError.Switch(

                    errors =>
                    {
                        // Ajouter les autres messages d'erreur au dessus du prompt
                        if (errors.Count > 0)
                        {
                            currentLine.LinkNewTextBlock("Error messages", errors.Join(' '));

                            Entity entity = _ecs.NewEntity("Input prompt and command");
                            currentLine = entity.AddComponent<LineComponent>();
                            Input.PromptLines.Add(currentLine);
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

        Entity entity = _ecs.NewEntity("Input prompt and command");
        currentLine = entity.AddComponent<LineComponent>();
        Input.PromptLines.Add(currentLine);

        var textInError = currentLine.LinkNewTextBlock("Text with parsing error", _text);
        currentLine.LinkNewTextHighlight(textInError, lexicalError.SyntaxError.Line, lexicalError.SyntaxError.Column);
    }

    private void HandleMidLineSyntaxError(SyntaxError syntaxError, LineComponent currentLine)
    {
        FillLineWithSyntaxGuides(syntaxError, currentLine);

        Entity entity = _ecs.NewEntity("Input prompt and command");
        currentLine = entity.AddComponent<LineComponent>();
        Input.PromptLines.Add(currentLine);

        var textInError = currentLine.LinkNewTextBlock("Text with parsing error", _text);
        currentLine.LinkNewTextHighlight(textInError, syntaxError.Line, syntaxError.Column);
    }

    private void HandleEndOfLineSyntaxError(SyntaxError syntaxError, LineComponent currentLine)
    {
        ShowIdentifierSuggestions(_text, currentLine);

        FillLineWithSyntaxGuides(syntaxError, currentLine);

        Entity entity = _ecs.NewEntity("Input prompt and command");
        currentLine = entity.AddComponent<LineComponent>();
        Input.PromptLines.Add(currentLine);

        var textInError = currentLine.LinkNewTextBlock("Text with parsing error", _text);
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

}
