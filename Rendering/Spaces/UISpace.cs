
using System.Drawing;

namespace Rendering.Spaces;

public class UISpace
{
    public Point TransformFromScreenSpace(Point point)
    {
        return new Point(point.X, point.Y);
    }
}
