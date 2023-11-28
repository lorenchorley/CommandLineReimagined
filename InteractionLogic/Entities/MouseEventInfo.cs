using System.Drawing;

namespace Interaction.Entities;

public class MouseEventInfo
{
    public bool Handled { get; set; } = false;
    public MouseButtonState ButtonState { get; init; }
    public MouseButton ChangedButton { get; init; }
    public int ClickCount { get; init; }
    public Point MousePosition { get; init; }
}
