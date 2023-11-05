using EntityComponentSystem;
using System.Drawing;

namespace Rendering.Components;

public interface IPositioningBehaviour
{
    void Position(Graphics gfx, Renderer renderer, RectangleF bounds);
}
