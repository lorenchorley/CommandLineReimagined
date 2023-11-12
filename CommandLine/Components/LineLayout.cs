using Console.Components;
using EntityComponentSystem;
using Rendering.Components;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;

namespace UIComponents.Compoents.Console;

public class LineLayout : UILayoutComponent
{
    public override IEnumerable<(string, string)> SerialisableDebugProperties => throw new NotImplementedException();

    public override void RecalculateChildTransforms()
    {
        throw new NotImplementedException();
    }
}
