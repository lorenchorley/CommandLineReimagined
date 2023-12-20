using UIComponents.Components;
using EntityComponentSystem;
using Rendering.Components;
using System.Drawing;
using System.Numerics;
using GOLD;
using EntityComponentSystem.Attributes;
using Rendering.Spaces;
using System.Collections.Generic;

namespace UIComponents.Compoents.Console;

public class ConsolePanel : UILayoutComponent
{
    //[State] public virtual float Height { get; set; } = 0;
    //[State] public virtual List<LineComponent> Lines { get; set; }

    [Inject] public virtual ECS ECS { get; set; }
    [Inject] public virtual ConceptualUISpace UISpace { get; set; }
    [Inject] public virtual PhysicalScreenSpace ScreenSpace { get; set; }

    private UITransform _transform;
    private UICamera _camera;

    public IEnumerable<LineComponent> Lines
    {
        get
        {
            List<LineComponent> list = new();
            foreach (var child in Entity.Children)
            {
                var line = child.TryGetComponent<LineComponent>();
                if (line != null)
                    list.Add(line);
            }
            return list;
        }
    }

    public override void OnInit()
    {
        //Lines = new();

        EnsureDependency<Renderer>();
        _transform = EnsureDependency<UITransform>();
    }

    public override void OnStart()
    {
        _camera = ECS.SearchForEntityWithComponent<UICamera>("MainCamera") ?? throw new Exception("No UICamera found");
    }

    public override void RecalculateChildTransforms()
    {
        PointF position;
        float calculatedWidth;
        SizeF size;

        float height = 0;
        float width = _transform.Size.Width; // La largeur donnée par le layout parent

        var letterSize = ScreenSpace.TransformSizeToUISpace(_camera.LetterSize);
        var letterWidth = letterSize.Width;
        var letterHeight = letterSize.Height;

        float verticalOffset = _transform.Position.Y;
        float horizontalOffset = _transform.Position.X; // TODO cette valeur n'est pas exploitée pour faire des retours à la ligne
        PointF NewPosition(float x, float y) => new(x, _transform.Size.Height - y);

        // On place les composants rélativement à ce container, et pas absoluement dans l'espace
        // Les coordonnées sont dans l'espace UI dont dans un rectangle de taille (1,1)
        foreach (var line in Lines)
        {
            foreach (var lineSegment in line.LineSegments)
            {
                //Height += lineSegment.Height; // How to get the line height at this stage in UI space ?

                UITransform transform = lineSegment.Entity.GetComponent<UITransform>();
                //Renderer renderer = lineSegment.Entity.GetComponent<Renderer>();
                //renderer.IsVisible = true; // Marquer comme visible pour le rendu, toujours nécessaire ?

                if (lineSegment is TextComponent textBlock)
                {
                    var newLineCount = textBlock.Text.Where(c => c == '\n').Count();
                    verticalOffset += _camera.LetterSize.Width * newLineCount; // Ajouter de l'hauteur s'il y a des retours à la ligne

                    position = NewPosition(horizontalOffset, verticalOffset);
                    calculatedWidth = letterWidth * textBlock.Text.Length;
                    size = new(calculatedWidth, letterHeight * (newLineCount + 1));
                    horizontalOffset += size.Width;

                    transform.Position = position;
                    transform.Size = size;
                    //renderer.CanvasRenderPosition = new(position, size);
                    //renderer.RenderingBehaviour = new TextRenderer(textBlock.Text, textBlock.Highlighted);

                    continue;
                }

                if (lineSegment is HighlightComponent highlight)
                {
                    // Positionner le rectangle de highlight afin que ça correspond à Line et Column du texte
                    var textRenderer = highlight.TextComponent.GetComponent<Renderer>();

                    var horizontalLetterOffset = highlight.Column * _camera.LetterSize.Width;
                    var verticalLetterOffset = highlight.Line * _camera.LetterSize.Width;

                    var trailingLineLength = GetTrailingLineLength(highlight.TextComponent.Text, highlight.Line, highlight.Column);

                    //position = new PointF(textRenderer.CanvasRenderPosition.Left + horizontalLetterOffset, textRenderer.CanvasRenderPosition.Top + verticalLetterOffset);
                    position = new PointF(horizontalLetterOffset, verticalLetterOffset);
                    size = new(_camera.LetterSize.Width, _camera.LetterSize.Width);

                    transform.Position = position;
                    transform.Size = size;
                    //renderer.CanvasRenderPosition = new(position, size);
                    //renderer.ZIndex = -1;
                    //renderer.RenderingBehaviour = new HighlightRenderer(textRenderer, trailingLineLength, _camera.LetterWidth, _camera.LetterHeight);

                    continue;
                }

                if (lineSegment is ButtonComponent button)
                {
                    // TODO Gérer les retours à la ligne
                    horizontalOffset += _camera.LetterSize.Width;

                    position = NewPosition(horizontalOffset, verticalOffset);
                    calculatedWidth = _camera.LetterSize.Width * button.Text.Length;
                    size = new(calculatedWidth, _camera.LetterSize.Width);

                    horizontalOffset += _camera.LetterSize.Width;
                    horizontalOffset += size.Width;

                    transform.Position = position;
                    transform.Size = size;
                    //renderer.CanvasRenderPosition = new(position, size);
                    //renderer.RenderingBehaviour = new ButtonRenderer(button.Text);

                    continue;
                }

                if (lineSegment is CursorComponent cursor)
                {
                    if (cursor.TextComponentReference == null)
                        continue;

                    // Positionner le rectangle de highlight afin que ça correspond à Line et Column du texte
                    var textRenderer = cursor.TextComponentReference.GetComponent<Renderer>();
                    var (lineNumber, columnNumber) = GetLineAndColumnNumberFromString(cursor, cursor.Text);

                    var horizontalLetterOffset = columnNumber * _camera.LetterSize.Width;
                    var verticalLetterOffset = lineNumber * _camera.LetterSize.Width;

                    //position = new PointF(textRenderer.CanvasRenderPosition.Left + horizontalLetterOffset, textRenderer.CanvasRenderPosition.Top + verticalLetterOffset);
                    position = new PointF(horizontalLetterOffset, verticalLetterOffset);
                    size = new(2, _camera.LetterSize.Width);

                    transform.Position = position;
                    transform.Size = size;
                    //renderer.CanvasRenderPosition = new(position, size);
                    //renderer.ZIndex = 1;
                    //renderer.RenderingBehaviour = new CursorRenderer(textRenderer, Input.IsCommandExecutable);

                    continue;
                }
            }
        }

        _transform.Size = new SizeF(_transform.Size.Width, height);
    }

    private static int GetTrailingLineLength(string text, int lineNumber, int columnNumber)
    {
        var textLines = text.Replace("\r", "").Split('\n');
        var line = textLines[lineNumber];

        return line.Length - columnNumber;
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

}
