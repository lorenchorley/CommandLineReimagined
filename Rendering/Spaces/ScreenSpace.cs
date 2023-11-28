
using System.Drawing;

namespace Rendering.Spaces;

public class ScreenSpace
{
    public Point TransformFromUISpace(Point point)
    {
        return new Point(point.X, point.Y);
    }
}
