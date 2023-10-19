﻿using CommandLineReimagined.Console.Entities;
using System.Collections.Generic;
using System.Drawing;

namespace CommandLineReimagine.Console.Components;

public class Renderer : Component
{

    public RectangleF CanvasRenderPosition { get; set; }
    public bool IsVisible { get; set; } = false;

    override protected void InsureDependencies()
    {
        Entity.TryAddComponent<UITransform>();
    }

    public override IEnumerable<(string, string)> SerialisableDebugProperties
    {
        get
        {
            yield return ("CanvasRenderPosition", CanvasRenderPosition.ToString());
            yield return ("IsVisible", IsVisible.ToString());
        }
    }

}
