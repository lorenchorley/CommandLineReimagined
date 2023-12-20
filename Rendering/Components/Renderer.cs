using EntityComponentSystem;
using System.Drawing;

namespace Rendering.Components;

public class Renderer : Component, IComparable<Renderer>
{
    [State]
    public virtual RectangleF CanvasRenderPosition { get; set; }
    [State]
    public virtual bool IsVisible { get; set; } = false;
    [State]
    public virtual int ZIndex { get; set; } = 0;
    public IRenderableComponent? RenderingBehaviour { get; set; }

    public UITransform UITransform { get; set; }

    public int CompareTo(Renderer? other)
    {
        if (other == null)
        {
            return 0;
        }

        return ZIndex - other.ZIndex;
    }

    public override void OnInit()
    {
        UITransform = EnsureDependency<UITransform>();
    }

    public override void OnDestroy()
    {
    }

    public IPositioningBehaviour? Positioner { get; set; }
}
