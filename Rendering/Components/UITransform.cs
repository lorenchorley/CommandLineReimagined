using EntityComponentSystem;
using System.Numerics;

namespace CommandLineReimagined.Rendering.Components;

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
