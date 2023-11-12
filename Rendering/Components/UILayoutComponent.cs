using EntityComponentSystem;
using System.Drawing;

namespace Rendering.Components;

public abstract class UILayoutComponent : Component
{
    public abstract void RecalculateChildTransforms();
}
