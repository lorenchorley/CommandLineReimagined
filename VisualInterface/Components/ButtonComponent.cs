using Rendering.Components;
using EntityComponentSystem;
using System.Drawing;

namespace UIComponents.Components
{
    public class ButtonComponent : LineSegmentComponent, IRenderableComponent
    {
        [State]
        public virtual string Text { get; set; }
        
        public override string ToText()
        {
            return Text;
        }

        public override void OnInit()
        {
            EnsureDependency<Renderer>();
            EnsureDependency<InteractiveComponent>();
        }

        public void Render(Graphics gfx, Renderer renderer, RectangleF bounds)
        {
            throw new NotImplementedException();
        }
    }
}

public struct ButtonRenderer : IRenderableComponent
{
    public string Text { get; set; }

    public Brush TextColor { get; internal set; } = new SolidBrush(Color.White);
    private Font _font = new Font(FontFamily.GenericMonospace, 14);
    private Brush _inputBackgroundBrush = new SolidBrush(Color.RoyalBlue);

    public ButtonRenderer(string text)
    {
        Text = text;
    }

    public void Render(Graphics gfx, Renderer renderer, RectangleF bounds)
    {

        gfx.FillRectangle(_inputBackgroundBrush, bounds);
        gfx.DrawString(Text, _font, TextColor, bounds.Location);

    }
}
