using EntityComponentSystem;
using System.Drawing;

namespace Rendering.Components;

public interface IRenderingBehaviour
{
    void Render(Graphics gfx, Renderer renderer, RectangleF bounds);
}
