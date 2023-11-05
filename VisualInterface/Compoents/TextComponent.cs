using Rendering.Components;
using EntityComponentSystem;
using System.Drawing;
using static System.Net.Mime.MediaTypeNames;

namespace Console.Components;

public class TextComponent : Component, ILineSegment
{
    public string Text { get; set; }
    public bool Highlighted { get; set; } = false;

    public string ToText()
    {
        return Text;
    }

    protected override void InsureDependencies()
    {
        Entity.TryAddComponent<Renderer>();
    }

    public override IEnumerable<(string, string)> SerialisableDebugProperties
    {
        get
        {
            yield return ("Text", Text);
        }
    }

}

//public struct TextPositioner : IPositioningBehaviour
//{
//    public TextComponent TextComponent { get; }

//    public TextPositioner(TextComponent textComponent)
//    {
//        TextComponent = textComponent;
//    }

//    public (int horizontalOffset, int verticalOffset) Position(Renderer renderer)
//    {
//        // TODO Gérer les retours à la ligne
//        var newLineCount = TextComponent.Text.Where(c => c == '\n').Count();
//        int verticalOffset += _letterHeight * newLineCount; // Ajouter de l'hauteur s'il y a des retours à la ligne

//        position = NewPosition(horizontalOffset, verticalOffset);
//        calculatedWidth = _letterWidth * textBlock.Text.Length;
//        size = new(calculatedWidth, _letterHeight * (newLineCount + 1));
//        renderer.CanvasRenderPosition = new(position, size);
//        horizontalOffset += size.Width;

//        renderer.RenderingBehaviour = new TextRenderer(TextComponent.Text);


//    }
//}

public struct TextRenderer : IRenderingBehaviour
{
    public string Text { get; set; }
    public bool Highlighted { get; set; }

    public Brush TextColor { get; internal set; } = new SolidBrush(Color.White);
    private Font _font = new Font(FontFamily.GenericMonospace, 14);

    public TextRenderer(string text, bool highlighted)
    {
        Text = text;
        Highlighted = highlighted;
    }

    public void Render(Graphics gfx, Renderer renderer, RectangleF bounds)
    {
        if (Highlighted)
        {
            gfx.FillRectangle(new SolidBrush(Color.FromArgb(200, Color.IndianRed)), bounds);
        }

        gfx.DrawString(Text, _font, TextColor, bounds.Location);
    }
}
