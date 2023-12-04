using EntityComponentSystem;
using System.Drawing;
using System.Numerics;

namespace Rendering.Components;

public class UITransform : Component
{
    [State]
    public virtual Vector2 Position { get; set; }
    [State]
    public virtual Vector2 Size { get; set; }

    public RectangleF GetBoundingBox()
    {
        return new RectangleF(Position.X, Position.Y, Size.X, Size.Y);
    }
}
