using EntityComponentSystem;
using Rendering.Components;
using System.Drawing;

namespace Console.Components;

public class TextComponentDifferenceObject
{
    public string? Text;
    public bool? Highlighted;
    public void ApplyTo(TextComponent textComponent)
    {
        if (Text is not null)
        {
            textComponent.Text = Text;
        }
        if (Highlighted is not null)
        {
            textComponent.Highlighted = Highlighted.Value;
        }
    }
}

public class TextComponentProxy : TextComponent
{
    public Func<TextComponentDifferenceObject> GetCurrentDifference { get; init; }

    public string _text;
    public override string Text
    {
        get
        {
            return _text;
        }
        set
        {
            _text = value;
            GetCurrentDifference().Text = value;
        }
    }

    public bool _highlighted;
    public override bool Highlighted
    {
        get
        {
            return _highlighted;
        }
        set
        {
            _highlighted = value;
            GetCurrentDifference().Highlighted = value;
        }
    }
}

public class TextComponent : LineSegmentComponent
{
    [State] public virtual string Text { get; set; }
    [State] public virtual bool Highlighted { get; set; } = false;

    public override string ToText()
    {
        return Text;
    }

    public override void OnInit()
    {
        EnsureDependency<Renderer>();
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
