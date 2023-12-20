using System.Drawing;
using System.Numerics;

namespace Utils.Math;
public class MathFEx
{
    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    public static PointF Lerp(PointF a, PointF b, float t)
    {
        return new PointF(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t));
    }
}
