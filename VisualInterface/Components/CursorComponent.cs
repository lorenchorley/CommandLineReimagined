using Rendering.Components;
using EntityComponentSystem;
using System.Drawing;

namespace UIComponents.Components;

public class CursorComponent : LineSegmentComponent
{
    [State] public int Position { get; set; }
    [State] public TextComponent TextComponentReference { get; set; }
    [State] public string Text{ get; set; }

    public override string ToText()
    {
        return Position.ToString();
    }

    public override void OnInit()
    {
        EnsureDependency<Renderer>();
    }

    public override IEnumerable<(string, string)> SerialisableDebugProperties
    {
        get
        {
            yield return ("Position", Position.ToString());
        }
    }

}

public struct CursorRenderer : IRenderingBehaviour
{
    public Renderer TextRenderer { get; }
    public bool IsCommandExecutable { get; set; }

    public Brush ExecutableFillColor { get; internal set; } = new SolidBrush(Color.White);
    public Brush FillColor { get; internal set; } = new SolidBrush(Color.Black);
    public Brush SelectionFillColor { get; internal set; } = new SolidBrush(Color.White);

    public CursorRenderer(Renderer textRenderer, bool isCommandExecutable)
    {
        TextRenderer = textRenderer;
        IsCommandExecutable = isCommandExecutable;
    }

    public void Render(Graphics gfx, Renderer renderer, RectangleF bounds)
    {
        bounds.Offset(TextRenderer.CanvasRenderPosition.Location);
        if (IsCommandExecutable)
        {
            gfx.FillRectangle(ExecutableFillColor, bounds);
        }
        else
        {
            gfx.FillRectangle(FillColor, bounds);
        }
    }
}
