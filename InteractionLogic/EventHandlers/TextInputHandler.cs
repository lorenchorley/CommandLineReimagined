using CommandLine.Modules;
using UIComponents;
using EntityComponentSystem;
using InteractionLogic.FrameworkAccessors;
using Rendering;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Terminal;
using UIComponents.Compoents.Console;
using Controller;

namespace InteractionLogic.EventHandlers;

public class TextInputHandler
{
    private readonly InputAccessor _inputAccessor;
    private readonly ECS _ecs;
    private readonly PathModule _pathModule;
    private readonly LoopController _loopController;
    private readonly Shell _shell;
    private readonly CommandHistoryModule _commandHistoryModule;
    private readonly Prompt _prompt;

    private ConsoleLayout ConsoleLayout { get; set; }

    public TextInputHandler(InputAccessor inputAccessor, ECS ecs, PathModule pathModule, LoopController loopController, Shell shell, CommandHistoryModule commandHistoryModule, Prompt promptPanel)
    {
        _inputAccessor = inputAccessor;
        _ecs = ecs;
        _pathModule = pathModule;
        _loopController = loopController;
        _shell = shell;
        _commandHistoryModule = commandHistoryModule;
        _prompt = promptPanel;
        _inputAccessor.RegisterEventHandlers<TextInputHandler>(RegisterEventHandlers, UnregisterEventHandlers);

        _shell.OnInit += Shell_OnInit;
    }

    private void Shell_OnInit(object? sender, EventArgs e)
    {
        ConsoleLayout = _ecs.SearchForEntityWithComponent<ConsoleLayout>("Layout") ?? throw new Exception("No console layout found");
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
    //    _loopController.RequestLoop();
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
        if (ConsoleLayout.Input.SelectionStart != _inputAccessor.Input.SelectionStart ||
            ConsoleLayout.Input.SelectionLength != _inputAccessor.Input.SelectionLength)
            _loopController.RequestLoop();

        // Extraction de la position du curseur pour le rendre dans la console
        ConsoleLayout.Input.SelectionStart = _inputAccessor.Input.SelectionStart;
        ConsoleLayout.Input.SelectionLength = _inputAccessor.Input.SelectionLength;

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

                _loopController.RequestLoop();
                return;
            }

            // Sinon il faut qu'on détermine si la commande est exécutable pour continuer
            //analysis = _shell.AnalyseCommand(_inputAccessor.Input.Text);
            //if (analysis.IsT0)
            //{
            ExecuteActiveLine();
            e.Handled = true; // TODO Needs mirroring logic in KeyDown to stop the text jumping up and then disappearing
                              //_loopController.RequestLoop();
            return;
            //}

            // TODO Gérer les erreurs de syntaxe et de vérification de type

            // Si la commande n'est exécutable à ce stade, on laisse la frappe de clé se faire
            //_loopController.RequestLoop();
            //return;
        }

        if (e.Key == Key.Z &&
            IsModifierPressed(ModifierKeys.Control) &&
            IsModifierPressed(ModifierKeys.Shift))
        {
            _commandHistoryModule.UndoLastCommand();
            e.Handled = true;
            _loopController.RequestLoop();
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
