using EntityComponentSystem;
using Rendering.Spaces;
using System.Drawing;
using System.Numerics;

namespace Rendering.Components;

public class UITransform : Component
{
    [State]
    public virtual PointF Position { get; set; }
    [State]
    public virtual SizeF Size { get; set; }

    public RectangleF GetBoundingBox()
    {
        return new RectangleF(Position.X, Position.Y, Size.Width, Size.Height);
    }

    internal RectangleF BoundsToScreenSpace(PhysicalScreenSpace physicalScreenSpace)
    {
        PointF point = physicalScreenSpace.TransformPointFromUISpace(Position);
        SizeF size = physicalScreenSpace.TransformSizeFromUISpace(Size);

        return new RectangleF(point, size);
    }
}
