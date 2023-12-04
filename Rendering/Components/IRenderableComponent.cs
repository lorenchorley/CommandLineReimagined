using EntityComponentSystem;
using System.Drawing;

namespace Rendering.Components;

public interface IRenderableComponent
{
    void Render(Graphics gfx, Renderer renderer, RectangleF bounds);
}
