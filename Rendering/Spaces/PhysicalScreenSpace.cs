
using System.Drawing;
using System.Numerics;

namespace Rendering.Spaces;

/// <summary>
/// Physical screen space is the what represents the canvas that should be drawn to.
/// The zero point of this space is the top left corner of the screen so that it corresponds to how a canvas is drawn.
/// </summary>
public class PhysicalScreenSpace
{
    public Vector2 TopLeft { get; private set; }
    public Vector2 TopRight { get; private set; }
    public Vector2 BottomLeft { get; private set; }
    public Vector2 BottomRight { get; private set; }

    private float _width;
    private float _height;

    public void AssertWithinUIBounds(Vector2 screenPoint)
    {
        if (screenPoint.X > _width || screenPoint.Y > _height || screenPoint.X < 0 || screenPoint.Y < 0)
        {
            throw new ArgumentException("Point not coming from screen space");
        }
    }

    public void SetSize(float width, float height)
    {
        _width = width;
        _height = height;

        TopLeft = new(0, 0);
        TopRight = new(_width, 0);
        BottomLeft = new(0, _height);
        BottomRight = new(_width, _height);
    }

    public Vector2 TransformFromUISpace(Vector2 uiPoint)
    {
        // Verify that the incoming point is correct
        ConceptualUISpace.AssertWithinUIBounds(uiPoint);

        return new(uiPoint.X * _width, (1 - uiPoint.Y) * _height);
    }

    public Vector2 TransformToUISpace(Vector2 screenPoint)
    {
        // Verify that the incoming point is correct
        AssertWithinUIBounds(screenPoint);

        //return new(screenPoint.X * _width, (1 - screenPoint.Y) * _height); // TODO
        return new(screenPoint.X / _width, 1 - screenPoint.Y / _height);
    }
}
