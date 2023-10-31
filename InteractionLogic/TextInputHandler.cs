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

    public TextInputHandler(InputAccessor inputAccessor, ConsoleLayout consoleRenderer, ECS ecs, PathModule pathModule, RenderLoop renderLoop, Shell shell, CommandHistoryModule commandHistoryModule)
    {
        _inputAccessor = inputAccessor;
        _consoleRenderer = consoleRenderer;
        _ecs = ecs;
        _pathModule = pathModule;
        _renderLoop = renderLoop;
        _shell = shell;
        _commandHistoryModule = commandHistoryModule;

        _inputAccessor.RegisterEventHandlers<TextInputHandler>(RegisterEventHandlers, UnregisterEventHandlers);
    }

    private void RegisterEventHandlers(TextBox input)
    {
        input.TextChanged += Input_TextChanged;
        input.Loaded += Input_Loaded;
        input.LostFocus += Input_LostFocus;
        input.KeyDown += Input_KeyUp;
    }

    private void UnregisterEventHandlers(TextBox input)
    {
        input.TextChanged -= Input_TextChanged;
        input.Loaded -= Input_Loaded;
        input.LostFocus -= Input_LostFocus;
        input.KeyDown -= Input_KeyUp;
    }

    public void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshInput(e.Changes.Count > 0);
    }

    public void RefreshInput(bool textChanged)
    {
        // Extraction du texte pour le rendre dans la console
        _consoleRenderer.Input.ActiveLine.Clear();
        Entity entity = _ecs.NewEntity("Input prompt");

        var textBlock = entity.AddComponent<Console.Components.TextComponent>();
        textBlock.Text = _pathModule.CurrentFolder + "> ";
        _consoleRenderer.Input.ActiveLine.AddLineSegment(textBlock);

        entity = _ecs.NewEntity("Input Textblock");
        textBlock = entity.AddComponent<Console.Components.TextComponent>();
        textBlock.Text = _inputAccessor.Input.Text;
        _consoleRenderer.Input.ActiveLine.AddLineSegment(textBlock);

        if (textChanged)
            _renderLoop.RefreshOnce();
    }

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
        // S'il y a besoin de rafraîchir l'affichage à cause d'une modification de la sélection
        if (_consoleRenderer.Input.SelectionStart != _inputAccessor.Input.SelectionStart ||
            _consoleRenderer.Input.SelectionLength != _inputAccessor.Input.SelectionLength)
            _renderLoop.RefreshOnce();

        // Extraction de la position du curseur pour le rendre dans la console
        _consoleRenderer.Input.SelectionStart = _inputAccessor.Input.SelectionStart;
        _consoleRenderer.Input.SelectionLength = _inputAccessor.Input.SelectionLength;

        Debug.WriteLine(_inputAccessor.Input.Text);

        if (e.Key == Key.Enter)
        {
            // Si maj est enfoncé, on va forcément à la ligne à la place d'exécuter la commande
            if (IsModifierPressed(ModifierKeys.Shift))
            {
                // On laisse la frapper de clé se faire
                _renderLoop.RefreshOnce();
                return;
            }

            // Sinon il faut qu'on détermine si la commande est exécutable pour continuer
            if (_shell.IsCommandExecutable(_inputAccessor.Input.Text))
            {
                ExecuteActiveLine();
                e.Handled = true; // TODO Needs mirroring logic in KeyDown to stop the text jumping up and then disappearing
                _renderLoop.RefreshOnce();
                return;
            }

            // Si la commande n'est exécutable à ce stade, on laisse la frappe de clé se faire
            _renderLoop.RefreshOnce();
            return;
        }

        if (e.Key == Key.Z &&
            IsModifierPressed(ModifierKeys.Control) &&
            IsModifierPressed(ModifierKeys.Shift))
        {
            _commandHistoryModule.UndoLastCommand();
            RefreshInput(false);
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
        _shell.ExecuteCommand(_inputAccessor.Input.Text);
        _inputAccessor.Input.Text = "";
    }
}
