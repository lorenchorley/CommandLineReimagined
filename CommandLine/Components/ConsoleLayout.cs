using UIComponents.Components;
using EntityComponentSystem;
using EntityComponentSystem.Attributes;
using Rendering.Components;
using System.Drawing;
using UIComponents.Compoents.Console;

namespace UIComponents;

public class ConsoleLayout : UILayoutComponent
{
    public UICamera Camera { get; private set; }
    public ConsoleInputPanel Input { get; private set; }
    public ConsoleOutputPanel Output { get; private set; }

    [Inject]
    public ECS ECS { get; init; }

    public UITransform Transform { get; private set; }

    public override IEnumerable<(string, string)> SerialisableDebugProperties => throw new NotImplementedException();

    public override void OnInit()
    {
        Transform = EnsureDependency<UITransform>();

        Camera = ECS.SearchForEntityWithComponent<UICamera>("MainCamera") ?? throw new Exception("No camera found");
        Output = ECS.SearchForEntityWithComponent<ConsoleOutputPanel>("Output") ?? throw new Exception("No output panel found");
        Input = ECS.SearchForEntityWithComponent<ConsoleInputPanel>("Input") ?? throw new Exception("No input panel found");
    }

    private IEnumerable<LineComponent> GetLines()
    {
        //foreach (var line in Input.PromptLines)
        for (int i = Input.PromptLines.Count - 1; i >= 0; i--)
        {
            yield return Input.PromptLines[i];
        }

        //foreach (var line in Output.Lines)
        for (int i = Output.Lines.Count - 1; i >= 0; i--)// TODO Make this operation async safe
        {
            yield return Output.Lines[i];
        }
    }

    // One of the only things that should be part of a layout behaviour
    public override void RecalculateChildTransforms()
    {
        List<LineComponent> lines =
            Input.Entity
                 .Children
                 .Select(c => c.GetComponent<LineComponent>())
                 .ToList();


        PointF position;
        float calculatedWidth;
        SizeF size;

        float verticalOffset = Transform.Position.Y;
        float horizontalOffset = Transform.Position.X; // TODO cette valeur n'est pas exploitée pour faire des retours à la ligne

        PointF NewPosition(float x, float y) => new PointF(x, Transform.Size.Y - y);

        for (int i = 0; i < lines.Count; i++)
        {
            LineComponent line = lines[i];

            foreach (LineSegmentComponent lineSegment in line.LineSegments)
            {
                Renderer renderer = ((Component)lineSegment).Entity.GetComponent<Renderer>();
                renderer.IsVisible = true; // Marquer comme visible pour le rendu

                if (lineSegment is TextComponent textBlock)
                {
                    // TODO Gérer les retours à la ligne
                    var newLineCount = textBlock.Text.Where(c => c == '\n').Count();
                    verticalOffset += Camera.LetterHeight * newLineCount; // Ajouter de l'hauteur s'il y a des retours à la ligne

                    position = NewPosition(horizontalOffset, verticalOffset);
                    calculatedWidth = Camera.LetterWidth * textBlock.Text.Length;
                    size = new(calculatedWidth, Camera.LetterHeight * (newLineCount + 1));
                    renderer.CanvasRenderPosition = new(position, size);
                    horizontalOffset += size.Width;

                    renderer.RenderingBehaviour = new TextRenderer(textBlock.Text, textBlock.Highlighted);

                    continue;
                }

                if (lineSegment is HighlightComponent highlight)
                {
                    // Positionner le rectangle de highlight afin que ça correspond à Line et Column du texte
                    var textRenderer = highlight.TextComponent.GetComponent<Renderer>();

                    var horizontalLetterOffset = highlight.Column * Camera.LetterWidth;
                    var verticalLetterOffset = highlight.Line * Camera.LetterHeight;

                    var trailingLineLength = GetTrailingLineLength(highlight.TextComponent.Text, highlight.Line, highlight.Column);

                    //position = new PointF(textRenderer.CanvasRenderPosition.Left + horizontalLetterOffset, textRenderer.CanvasRenderPosition.Top + verticalLetterOffset);
                    position = new PointF(horizontalLetterOffset, verticalLetterOffset);
                    size = new(Camera.LetterWidth, Camera.LetterHeight);
                    renderer.CanvasRenderPosition = new(position, size);
                    renderer.ZIndex = -1;

                    renderer.RenderingBehaviour = new HighlightRenderer(textRenderer, trailingLineLength, Camera.LetterWidth, Camera.LetterHeight);

                    continue;
                }

                if (lineSegment is ButtonComponent button)
                {
                    // TODO Gérer les retours à la ligne
                    horizontalOffset += Camera.LetterWidth;

                    position = NewPosition(horizontalOffset, verticalOffset);
                    calculatedWidth = Camera.LetterWidth * button.Text.Length;
                    size = new(calculatedWidth, Camera.LetterHeight);
                    renderer.CanvasRenderPosition = new(position, size);

                    horizontalOffset += Camera.LetterWidth;
                    horizontalOffset += size.Width;

                    renderer.RenderingBehaviour = new ButtonRenderer(button.Text);

                    continue;
                }

                if (lineSegment is CursorComponent cursor)
                {
                    if (cursor.TextComponentReference == null)
                        continue;

                    // Positionner le rectangle de highlight afin que ça correspond à Line et Column du texte
                    var textRenderer = cursor.TextComponentReference.GetComponent<Renderer>();
                    var (lineNumber, columnNumber) = GetLineAndColumnNumberFromString(cursor, cursor.Text);

                    var horizontalLetterOffset = columnNumber * Camera.LetterWidth;
                    var verticalLetterOffset = lineNumber * Camera.LetterHeight;

                    //position = new PointF(textRenderer.CanvasRenderPosition.Left + horizontalLetterOffset, textRenderer.CanvasRenderPosition.Top + verticalLetterOffset);
                    position = new PointF(horizontalLetterOffset, verticalLetterOffset);
                    size = new(2, Camera.LetterHeight);
                    renderer.CanvasRenderPosition = new(position, size);
                    renderer.ZIndex = 1;

                    renderer.RenderingBehaviour = new CursorRenderer(textRenderer, Input.IsCommandExecutable);

                    continue;
                }

            }

            // Si on a dépassé la largeur du canvas, on fait un retour à la ligne
            // Ce n'est pas une bonne façon de faire, car on dépasse déjà la largeur 
            // TODO
            //if (horizontalOffset > transform.Size.X)
            //{
            //    horizontalOffset += letterHeight;
            //    verticalOffset = 0;
            //}

            horizontalOffset = Transform.Position.X;
            verticalOffset += Camera.LetterHeight;

            // On a dépassé le canvas, on arrête
            if (verticalOffset > Transform.Size.Y)
            {
                break;
            }

        }
    }

    private static (int lineNumber, int columnNumber) GetLineAndColumnNumberFromString(CursorComponent cursor, string text)
    {
        var textLines = text.Replace("\r", "").Split('\n');

        int lineNumber = 0;
        int columnNumber = 0;
        int j = 0;
        while (j < text.Length && j < cursor.Position)
        {
            if (columnNumber >= textLines[lineNumber].Length)
            {
                lineNumber++;
                columnNumber = 0;
            }

            columnNumber++;
            j++;
        }

        return (lineNumber, columnNumber);
    }

    private static int GetTrailingLineLength(string text, int lineNumber, int columnNumber)
    {
        var textLines = text.Replace("\r", "").Split('\n');
        var line = textLines[lineNumber];

        return line.Length - columnNumber;
    }

}
