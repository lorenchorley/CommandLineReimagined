using CommandLine.Modules;
using Console;
using EntityComponentSystem;
using InteractionLogic.FrameworkAccessors;
using Rendering;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Terminal;

namespace InteractionLogic;

public class TextInputHandler
{
    private readonly InputAccessor _inputAccessor;
    private readonly ConsoleLayout _consoleRenderer;
    private readonly ECS _ecs;
    private readonly PathModule _pathModule;
    private readonly RenderLoop _renderLoop;
    private readonly Shell _shell;
    private readonly CommandHistoryModule _commandHistoryModule;
    private readonly Prompt _prompt;

    public TextInputHandler(InputAccessor inputAccessor, ConsoleLayout consoleRenderer, ECS ecs, PathModule pathModule, RenderLoop renderLoop, Shell shell, CommandHistoryModule commandHistoryModule, Prompt promptPanel)
    {
        _inputAccessor = inputAccessor;
        _consoleRenderer = consoleRenderer;
        _ecs = ecs;
        _pathModule = pathModule;
        _renderLoop = renderLoop;
        _shell = shell;
        _commandHistoryModule = commandHistoryModule;
        _prompt = promptPanel;
        _inputAccessor.RegisterEventHandlers<TextInputHandler>(RegisterEventHandlers, UnregisterEventHandlers);
    }

    private void RegisterEventHandlers(TextBox input)
    {
        input.TextChanged += Input_TextChanged;
        input.Loaded += Input_Loaded;
        input.LostFocus += Input_LostFocus;
        input.KeyDown += Input_KeyUp;
        input.SelectionChanged += Input_SelectionChanged;
    }

    private void UnregisterEventHandlers(TextBox input)
    {
        input.TextChanged -= Input_TextChanged;
        input.Loaded -= Input_Loaded;
        input.LostFocus -= Input_LostFocus;
        input.KeyDown -= Input_KeyUp;
        input.SelectionChanged -= Input_SelectionChanged;
    }

    private void Input_SelectionChanged(object sender, RoutedEventArgs e)
    {
        _prompt.SetCursorPosition(_inputAccessor.Input.SelectionStart);
    }

    public void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        _prompt.SetPromptText(_inputAccessor.Input.Text);
        //_prompt.SetCursorPosition(_inputAccessor.Input.SelectionStart);
        //bool textChanged = e.Changes.Count > 0;

        // Extraction du texte pour le rendre dans la console
        //_consoleRenderer.Input.ActiveLine.Clear();
        //Entity entity = _ecs.NewEntity("Input prompt");

        //var textBlock = entity.AddComponent<Console.Components.TextComponent>();
        //textBlock.Text = _pathModule.CurrentFolder + "> ";
        //_consoleRenderer.Input.ActiveLine.AddLineSegment(textBlock);

        //entity = _ecs.NewEntity("Input Textblock");
        //textBlock = entity.AddComponent<Console.Components.TextComponent>();
        //textBlock.Text = _inputAccessor.Input.Text;
        //_consoleRenderer.Input.ActiveLine.AddLineSegment(textBlock);

        //if (textChanged)
        //{
        //    RefreshPrompt();
        //}
    }

    //private void RefreshPrompt()
    //{
    //    _renderLoop.RefreshOnce();
    //}

    //private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
    //{
    //    //if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
    //    //{
    //    //    // Handle Ctrl+Z (Undo) here
    //    //    e.Handled = true; // Prevent the default Undo behavior
    //    //                      // Your custom Undo logic goes here
    //    //}
    //}

    public void Input_KeyUp(object sender, KeyEventArgs e)
    {
        CommandAnalysisResult analysis;

        // S'il y a besoin de rafraîchir l'affichage à cause d'une modification de la sélection
        if (_consoleRenderer.Input.SelectionStart != _inputAccessor.Input.SelectionStart ||
            _consoleRenderer.Input.SelectionLength != _inputAccessor.Input.SelectionLength)
            _renderLoop.RefreshOnce();

        // Extraction de la position du curseur pour le rendre dans la console
        _consoleRenderer.Input.SelectionStart = _inputAccessor.Input.SelectionStart;
        _consoleRenderer.Input.SelectionLength = _inputAccessor.Input.SelectionLength;

        _prompt.SetPromptText(_inputAccessor.Input.Text);

        if (e.Key == Key.Enter)
        {
            // Si maj est enfoncé, on va forcément à la ligne à la place d'exécuter la commande
            if (IsModifierPressed(ModifierKeys.Shift))
            {
                var previousStart = _inputAccessor.Input.SelectionStart;

                // Insérer le retour à la ligne où il y a le curseur
                var before = _inputAccessor.Input.Text.Substring(0, previousStart);
                var after = _inputAccessor.Input.Text.Substring(previousStart);
                _inputAccessor.Input.Text = before + "\n" + after;

                _inputAccessor.Input.SelectionStart = previousStart + 1; // Pour se positionner sur la nouvelle ligne

                _renderLoop.RefreshOnce();
                return;
            }

            // Sinon il faut qu'on détermine si la commande est exécutable pour continuer
            //analysis = _shell.AnalyseCommand(_inputAccessor.Input.Text);
            //if (analysis.IsT0)
            //{
                ExecuteActiveLine();
                e.Handled = true; // TODO Needs mirroring logic in KeyDown to stop the text jumping up and then disappearing
                //_renderLoop.RefreshOnce();
                return;
            //}

            // TODO Gérer les erreurs de syntaxe et de vérification de type

            // Si la commande n'est exécutable à ce stade, on laisse la frappe de clé se faire
            //_renderLoop.RefreshOnce();
            //return;
        }

        if (e.Key == Key.Z &&
            IsModifierPressed(ModifierKeys.Control) &&
            IsModifierPressed(ModifierKeys.Shift))
        {
            _commandHistoryModule.UndoLastCommand();
            e.Handled = true;
            _renderLoop.RefreshOnce();
            return;
        }
    }

    private static bool IsModifierPressed(ModifierKeys modifierKeys)
    {
        return (Keyboard.Modifiers & modifierKeys) == modifierKeys;
    }

    public void Input_LostFocus(object sender, RoutedEventArgs e)
    {
        // Maintenir le focus sur l'input
        _inputAccessor.Input.Focus();
    }

    public void Input_Loaded(object sender, RoutedEventArgs e)
    {
        // Mettre le focus sur l'input initialement
        _inputAccessor.Input.Focus();
    }

    public void ExecuteActiveLine()
    {
        if (_shell.ExecuteCurrentPrompt())
        {
            // Si la commande a été exécutée, on supprime le texte de l'input
            _inputAccessor.Input.Text = "";
        }
    }
}
