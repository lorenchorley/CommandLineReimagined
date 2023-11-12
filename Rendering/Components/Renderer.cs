﻿using EntityComponentSystem;
using System.Drawing;

namespace Rendering.Components;

public class Renderer : Component, IComparable<Renderer>
{
    public RectangleF CanvasRenderPosition { get; set; }
    public bool IsVisible { get; set; } = false;
    public int ZIndex { get; set; } = 0;
    public IRenderingBehaviour? RenderingBehaviour { get; set; }

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
        EnsureDependency<UITransform>();
    }

    public override void OnDestroy()
    {
    }

    public override IEnumerable<(string, string)> SerialisableDebugProperties
    {
        get
        {
            yield return ("CanvasRenderPosition", CanvasRenderPosition.ToString());
            yield return ("IsVisible", IsVisible.ToString());
        }
    }

    public IPositioningBehaviour? Positioner { get; set; }
}
