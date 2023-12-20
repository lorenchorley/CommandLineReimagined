using Application.FrameworkAccessors;
using EntityComponentSystem;
using InteractionLogic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Application.EventHandlers;

public class TextInputHandler : IECSSubsystem
{
    private readonly InputAccessor _inputAccessor;
    private readonly InputSystem _inputSystem;

    public TextInputHandler(InputAccessor inputAccessor, InputSystem inputSystem)
    {
        _inputAccessor = inputAccessor;
        _inputSystem = inputSystem;
        _inputAccessor.RegisterEventHandlers<TextInputHandler>(RegisterEventHandlers, UnregisterEventHandlers);
    }

    private void RegisterEventHandlers(TextBox input)
    {
        input.TextChanged += Input_TextChanged;
        input.Loaded += Input_Loaded;
        input.LostFocus += Input_LostFocus;
        input.KeyDown += Input_KeyDown;
        input.SelectionChanged += Input_SelectionChanged;
    }

    private void UnregisterEventHandlers(TextBox input)
    {
        input.TextChanged -= Input_TextChanged;
        input.Loaded -= Input_Loaded;
        input.LostFocus -= Input_LostFocus;
        input.KeyDown -= Input_KeyDown;
        input.SelectionChanged -= Input_SelectionChanged;
    }

    private void Input_SelectionChanged(object sender, RoutedEventArgs e)
    {
        _inputSystem.UpdateCursorPosition(_inputAccessor.Input.SelectionStart, _inputAccessor.Input.SelectionLength);
    }

    public void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        _inputSystem.UpdateEnteredText(_inputAccessor.Input.Text);
    }

    public void Input_KeyDown(object sender, KeyEventArgs e)
    {
        _inputSystem.UpdateCursorPosition(_inputAccessor.Input.SelectionStart, _inputAccessor.Input.SelectionLength);
        _inputSystem.OnKeyDown(new Interaction.Entities.KeyEventInfo() { Modifiers = (Interaction.Entities.ModifierKeys)(int)Keyboard.Modifiers, Key = (Interaction.Entities.Key)(int)e.Key });
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

    public void OnInit()
    {
    }

    public void OnStart()
    {
    }
}
