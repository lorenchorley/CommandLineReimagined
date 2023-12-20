using EntityComponentSystem;
using System.Drawing;
using System.Numerics;

namespace Rendering.Components;

public class UICamera : Component
{
    private readonly object _lock = new object();

    [State]
    public virtual Vector2 RenderSpaceSize { get; set; }

    private SizeF _letterSize;
    public SizeF LetterSize
    {
        get
        {
            lock (this)
            {
                return _letterSize;
            }
        }
        set
        {
            lock (this)
            {
                _letterSize = value;
            }
        }
    }
}
