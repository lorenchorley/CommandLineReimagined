using Rendering.Components;
using EntityComponentSystem;
using EntityComponentSystem.Serialisation;
using System.Drawing;
using UIComponents.Components;

namespace UIComponents.Compoents.Console;

public class ConsoleInputPanel : UILayoutComponent
{
    private Brush _inputBackgroundBrush = new SolidBrush(Color.RoyalBlue);

    [State] public List<LineComponent> PromptLines { get; set; } = new();
    [State] public int SelectionStart { get; set; }
    [State] public int SelectionLength { get; set; }
    [State] public bool IsCommandExecutable { get; set; }

    public override void OnInit()
    {
        EnsureDependency<Renderer>();
    }

    public void DrawBackgroundAroundLine(Graphics gfx, List<LineComponent> lines, float canvasWidth, float canvasHeight)
    {
        RectangleF boundingBox =
            PromptLines.SelectMany(s => s.LineSegments)
                 .OfType<TextComponent>()
                 .Select(s => s.GetComponent<Renderer>())
                 .Select(r => r.CanvasRenderPosition)
                 .Append(new RectangleF(new PointF(0, canvasHeight), new SizeF(canvasWidth, 0)))
                 .Aggregate((r, s) => RectangleF.Union(r, s));

        gfx.FillRectangle(_inputBackgroundBrush, boundingBox);
    }

    public override void RecalculateChildTransforms()
    {
        throw new NotImplementedException();
    }
}
