using EntityComponentSystem;
using System.Numerics;

namespace Rendering.Components;

public class UICamera : Component
{
    public Vector2 RenderSpaceSize { get; set; }
    public Vector2 LetterSize
    {
        get
        {
            return new Vector2(LetterWidth, LetterHeight);
        }
        set
        {
            LetterWidth = value.X;
            LetterHeight = value.Y;
        }
    }
    public float LetterHeight { get; private set; }
    public float LetterWidth { get; private set; }

    public override IEnumerable<(string, string)> SerialisableDebugProperties
    {
        get
        {
            yield return ("RenderSpaceSize", RenderSpaceSize.ToString());
            yield return ("LetterSize", LetterSize.ToString());
        }
    }

}
