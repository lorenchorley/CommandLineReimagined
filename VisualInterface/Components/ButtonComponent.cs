using Rendering.Components;
using Rendering.Interaction;
using EntityComponentSystem;
using System.Drawing;

namespace UIComponents.Components
{
    public class ButtonComponent : LineSegmentComponent
    {
        [State] public string Text { get; set; }

        public override IEnumerable<(string, string)> SerialisableDebugProperties
        {
            get
            {
                yield return ("Text", Text);
            }
        }

        
        public override string ToText()
        {
            return Text;
        }

        public override void OnInit()
        {
            EnsureDependency<Renderer>();
            EnsureDependency<InteractiveComponent>();
        }

    }
}

public struct ButtonRenderer : IRenderingBehaviour
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
