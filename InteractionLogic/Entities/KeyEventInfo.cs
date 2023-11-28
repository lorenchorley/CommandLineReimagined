
namespace Interaction.Entities;

public class KeyEventInfo
{
    public bool Handled { get; set; } = false;
    public ModifierKeys Modifiers { get; init; }
    public Key Key { get; init; }
    public bool IsModifierPressed(ModifierKeys modifierKey)
    {
        return (Modifiers & modifierKey) == modifierKey;
    }

}
