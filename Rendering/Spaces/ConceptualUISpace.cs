
using System.Drawing;
using System.Numerics;

namespace Rendering.Spaces;

public class ConceptualUISpace
{
    public static readonly Vector2 TopLeft = new(0, 1);
    public static readonly Vector2 TopRight = new(1, 1);
    public static readonly Vector2 BottomLeft = new(0, 0);
    public static readonly Vector2 BottomRight = new(1, 0);

    private readonly PhysicalScreenSpace _screenSpace;

    public ConceptualUISpace(PhysicalScreenSpace screenSpace)
    {
        _screenSpace = screenSpace;
    }

    public static void AssertWithinUIBounds(Vector2 uiPoint)
    {
        if (uiPoint.X > 1 || uiPoint.Y > 1 || uiPoint.X < 0 || uiPoint.Y < 0)
        {
            throw new ArgumentException("Point not coming from UI space");
        }
    }

    public Vector2 TransformFromScreenSpace(Vector2 screenPoint)
        => _screenSpace.TransformToUISpace(screenPoint);

}
