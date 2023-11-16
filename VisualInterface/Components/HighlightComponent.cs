using Rendering.Components;
using EntityComponentSystem;
using System.Drawing;

namespace UIComponents.Components;

public class HighlightComponent : LineSegmentComponent
{
    [State] public int Line { get; set; }
    [State] public int Column { get; set; }
    [State] public TextComponent TextComponent { get; set; }

    private static readonly Brush _fillColor = new SolidBrush(Color.Red);
    public Brush FillColor { get; internal set; } = _fillColor;

    public override void OnInit()
    {
        EnsureDependency<Renderer>();
    }

    public override string ToText()
    {
        return $"{Line}:{Column}";
    }

}

public struct HighlightRenderer : IRenderingBehaviour
{
    private readonly float _letterWidth;
    private readonly float _letterHeight;

    public Renderer TextRenderer { get; }
    public int TrailingTextOnLine { get; }

    public Brush[] FillColor { get; internal set; } = new Brush[]
    {
        new SolidBrush(Color.FromArgb(200, Color.IndianRed)), 
        new SolidBrush(Color.FromArgb(150, Color.IndianRed)), 
        new SolidBrush(Color.FromArgb(100, Color.IndianRed)), 
        new SolidBrush(Color.FromArgb(50, Color.IndianRed)), 
    };

    public HighlightRenderer(Renderer textRenderer, int trailingTextOnLine, float letterWidth, float letterHeight)
    {
        TextRenderer = textRenderer;
        TrailingTextOnLine = trailingTextOnLine;
        _letterWidth = letterWidth;
        _letterHeight = letterHeight;
    }

    public void Render(Graphics gfx, Renderer renderer, RectangleF bounds)
    {
        bounds.Offset(TextRenderer.CanvasRenderPosition.Location);
        for (int i = 0; i < TrailingTextOnLine && i <= 2; i++)
        {
            gfx.FillRectangle(FillColor[i], bounds);
            bounds.Offset(new PointF(_letterWidth, 0));
        }
    }
}
