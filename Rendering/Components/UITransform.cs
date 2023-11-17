using EntityComponentSystem;
using System.Numerics;

namespace Rendering.Components;

public class UITransform : Component
{
    [State]
    public virtual Vector2 Position { get; set; }
    [State]
    public virtual Vector2 Size { get; set; }
}
