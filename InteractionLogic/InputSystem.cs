using EntityComponentSystem;
using Interaction.Entities;

namespace InteractionLogic;

public class InputSystem : IECSSubsystem
{
    private readonly ECS _ecs;
    private int _selectionStart = -1;
    private int _selectionLength = -1;

    public InputSystem(ECS ecs)
    {
        _ecs = ecs;
    }

    public void OnInit()
    {
    }

    public void OnStart()
    {
    }

    public void UpdateCursorPosition(int selectionStart, int selectionLength)
    {
        if (_selectionStart == selectionStart && _selectionLength == selectionLength)
        {
            return;
        }

        _selectionStart = selectionStart;
        _selectionLength = selectionLength;

        foreach (var inputComponent in _ecs.GetComponents<InputComponent>())
        {
            inputComponent.OnCursorPositionChanged(selectionStart, selectionLength);
        }
    }

    public void UpdateEnteredText(string text)
    {
        foreach (var inputComponent in _ecs.GetComponents<InputComponent>())
        {
            inputComponent.OnTextChanged(text);
        }
    }

    public void OnKeyDown(KeyEventInfo eventInfo)
    {
        foreach (var inputComponent in _ecs.GetComponents<InputComponent>())
        {
            inputComponent.OnKeyDown(eventInfo);
        }
    }

    public void OnLeftClick(MouseEventInfo eventInfo)
    {
        foreach (var inputComponent in _ecs.GetComponents<InputComponent>())
        {
            inputComponent.OnLeftClick(eventInfo);
        }
    }

    public void OnDoubleLeftClick(MouseEventInfo eventInfo)
    {
        foreach (var inputComponent in _ecs.GetComponents<InputComponent>())
        {
            inputComponent.OnDoubleLeftClick(eventInfo);
        }
    }

    public void OnRightClick(MouseEventInfo eventInfo)
    {
        foreach (var inputComponent in _ecs.GetComponents<InputComponent>())
        {
            inputComponent.OnRightClick(eventInfo);
        }
    }

}
