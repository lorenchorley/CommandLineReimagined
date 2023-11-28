using Interaction.Entities;

namespace EntityComponentSystem;

public abstract class InputComponent : Component
{
    // Text events
    public virtual void OnKeyDown(KeyEventInfo eventInfo) { }
    public virtual void OnCursorPositionChanged(int selectionStart, int selectionLength) { }

    // Text box events
    public virtual void OnTextChanged(string text) { }

    // Screen events
    public virtual void OnScreenSizeChanged(int width, int height) { }

    // Mouse events
    public virtual void OnLeftClick(MouseEventInfo eventInfo) { }
    public virtual void OnDoubleLeftClick(MouseEventInfo eventInfo) { }
    public virtual void OnRightClick(MouseEventInfo eventInfo) { }

}
