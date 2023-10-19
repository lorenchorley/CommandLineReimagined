﻿using CommandLineReimagined.Console.Entities;
using System.Collections.Generic;

namespace CommandLineReimagine.Console.Components;

public class TextBlock : Component, ILineSegment
{
    public string Text { get; set; }

    public string ToText()
    {
        return Text;
    }

    protected override void InsureDependencies()
    {
        Entity.TryAddComponent<Renderer>();
    }

    public override IEnumerable<(string, string)> SerialisableDebugProperties
    {
        get
        {
            yield return ("Text", Text);
        }
    }

}
