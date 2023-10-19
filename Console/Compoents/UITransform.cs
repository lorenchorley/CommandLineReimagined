using CommandLineReimagined.Console.Entities;
using System.Collections.Generic;
using System.Numerics;
using CommandLineReimagine.Console.Interaction;

namespace CommandLineReimagine.Console.Components;

public class UITransform : Component
{
    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; }

    public override IEnumerable<(string, string)> SerialisableDebugProperties
    {
        get
        {
            yield return ("Position", Position.ToString());
            yield return ("Size", Size.ToString());
        }
    }

}
