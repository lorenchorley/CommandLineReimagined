
using System.Drawing;
using System.Numerics;

namespace Rendering.Spaces;

public class ConceptualUISpace
{
    public static readonly PointF TopLeft = new(0, 1);
    public static readonly PointF TopRight = new(1, 1);
    public static readonly PointF BottomLeft = new(0, 0);
    public static readonly PointF BottomRight = new(1, 0);

    private readonly PhysicalScreenSpace _screenSpace;

    public ConceptualUISpace(PhysicalScreenSpace screenSpace)
    {
        _screenSpace = screenSpace;
    }

    public static void AssertWithinUIBounds(PointF uiPoint)
    {
        if (uiPoint.X > 1 || uiPoint.Y > 1 || uiPoint.X < 0 || uiPoint.Y < 0)
        {
            throw new ArgumentException("Point not coming from UI space");
        }
    }

    public PointF TransformFromScreenSpace(PointF screenPoint)
        => _screenSpace.TransformToUISpace(screenPoint);

}
