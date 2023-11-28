using EntityComponentSystem;
using System.Numerics;

namespace Rendering.Components;

public class UICamera : Component
{
    [State]
    public virtual Vector2 RenderSpaceSize { get; set; }
    [State]
    public virtual float LetterHeight { get; set; }
    [State]
    public virtual float LetterWidth { get; set; }

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
}
