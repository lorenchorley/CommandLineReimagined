using UIComponents.Components;
using EntityComponentSystem;
using EntityComponentSystem.Attributes;
using Rendering.Components;
using System.Drawing;
using UIComponents.Compoents.Console;
using Rendering.Spaces;
using System.Numerics;

namespace UIComponents;

public class ConsoleLayout : UILayoutComponent, IRenderableComponent
{
    private Brush _inputBackgroundBrush = new SolidBrush(Color.RoyalBlue);

    public virtual UICamera Camera { get; set; }
    public virtual ConsolePanel Input { get; set; } // Fit horizontally, respect height
    public virtual UITransform InputTransform { get; set; }
    public virtual ConsolePanel Output { get; set; } // Fit horizontally, fill vertically
    public virtual UITransform OutputTransform { get; set; }

    [Inject] public ECS ECS { get; init; }
    [Inject] public ConceptualUISpace UISpace { get; init; }
    [Inject] public PhysicalScreenSpace ScreenSpace { get; init; }

    public UITransform Transform { get; private set; }

    public override void OnInit()
    {
        Transform = EnsureDependency<UITransform>();
    }

    public override void OnStart()
    {
        Camera = ECS.SearchForEntityWithComponent<UICamera>("MainCamera") ?? throw new Exception("No camera found");
        Input = ECS.SearchForEntityWithComponent<ConsolePanel>("Input") ?? throw new Exception("No input panel found");
        InputTransform = Input.GetComponent<UITransform>();
        Output = ECS.SearchForEntityWithComponent<ConsolePanel>("Output") ?? throw new Exception("No output panel found");
        OutputTransform = Output.GetComponent<UITransform>();
    }

    // One of the only things that should be part of a layout behaviour
    public override void RecalculateChildTransforms()
    {
        // Fill the screen with the transform of this component
        Transform.Position = ConceptualUISpace.BottomLeft;
        Transform.Size = new SizeF(1, 1);

        float height = InputTransform.Size.Height;

        // Verify that the input panel's height is set correctly and does not go beyond [0,1]
        if (height < 0 || height > 1)
        {
            throw new Exception($"Input height is not set correctly ({height})");
        }

        // Fit the input panel horizontally, respecting the given height, to the bottom of the ui space
        InputTransform.Position = ConceptualUISpace.BottomLeft;
        InputTransform.Size = new SizeF(1, height);

        // Fit horizontally and vertically the output panel to the remaining space
        OutputTransform.Position = new PointF(0, height);
        OutputTransform.Size = new SizeF(1, 1 - height);


        //return;

        //List<LineComponent> lines =
        //    Input.Entity // NullRef : parce qu'on est sur un objet shadow qui n'a pas des valeurs de propriétés ! Comment faire pour les UILayout dans ce cas ?
        //         .Children
        //         .ToArray()
        //         .Select(c => c.GetComponent<LineComponent>())
        //         .ToList();

        //PointF position;
        //float calculatedWidth;
        //SizeF size;

        //float verticalOffset = Transform.Position.Y;
        //float horizontalOffset = Transform.Position.X; // TODO cette valeur n'est pas exploitée pour faire des retours à la ligne

        //PointF NewPosition(float x, float y) => new(x, Transform.Size.Y - y);

        //for (int i = 0; i < lines.Count; i++)
        //{
        //    LineComponent line = lines[i];

        //    foreach (LineSegmentComponent lineSegment in line.LineSegments)
        //    {
        //        Renderer renderer = ((Component)lineSegment).Entity.GetComponent<Renderer>();
        //        renderer.IsVisible = true; // Marquer comme visible pour le rendu

        //        if (lineSegment is TextComponent textBlock)
        //        {
        //            // TODO Gérer les retours à la ligne
        //            var newLineCount = textBlock.Text.Where(c => c == '\n').Count();
        //            verticalOffset += Camera.LetterHeight * newLineCount; // Ajouter de l'hauteur s'il y a des retours à la ligne

        //            position = NewPosition(horizontalOffset, verticalOffset);
        //            calculatedWidth = Camera.LetterWidth * textBlock.Text.Length;
        //            size = new(calculatedWidth, Camera.LetterHeight * (newLineCount + 1));
        //            renderer.CanvasRenderPosition = new(position, size);
        //            horizontalOffset += size.Width;

        //            renderer.RenderingBehaviour = new TextRenderer(textBlock.Text, textBlock.Highlighted);

        //            continue;
        //        }

        //        if (lineSegment is HighlightComponent highlight)
        //        {
        //            // Positionner le rectangle de highlight afin que ça correspond à Line et Column du texte
        //            var textRenderer = highlight.TextComponent.GetComponent<Renderer>();

        //            var horizontalLetterOffset = highlight.Column * Camera.LetterWidth;
        //            var verticalLetterOffset = highlight.Line * Camera.LetterHeight;

        //            var trailingLineLength = GetTrailingLineLength(highlight.TextComponent.Text, highlight.Line, highlight.Column);

        //            //position = new PointF(textRenderer.CanvasRenderPosition.Left + horizontalLetterOffset, textRenderer.CanvasRenderPosition.Top + verticalLetterOffset);
        //            position = new PointF(horizontalLetterOffset, verticalLetterOffset);
        //            size = new(Camera.LetterWidth, Camera.LetterHeight);
        //            renderer.CanvasRenderPosition = new(position, size);
        //            renderer.ZIndex = -1;

        //            renderer.RenderingBehaviour = new HighlightRenderer(textRenderer, trailingLineLength, Camera.LetterWidth, Camera.LetterHeight);

        //            continue;
        //        }

        //        if (lineSegment is ButtonComponent button)
        //        {
        //            // TODO Gérer les retours à la ligne
        //            horizontalOffset += Camera.LetterWidth;

        //            position = NewPosition(horizontalOffset, verticalOffset);
        //            calculatedWidth = Camera.LetterWidth * button.Text.Length;
        //            size = new(calculatedWidth, Camera.LetterHeight);
        //            renderer.CanvasRenderPosition = new(position, size);

        //            horizontalOffset += Camera.LetterWidth;
        //            horizontalOffset += size.Width;

        //            renderer.RenderingBehaviour = new ButtonRenderer(button.Text);

        //            continue;
        //        }

        //        if (lineSegment is CursorComponent cursor)
        //        {
        //            if (cursor.TextComponentReference == null)
        //                continue;

        //            // Positionner le rectangle de highlight afin que ça correspond à Line et Column du texte
        //            var textRenderer = cursor.TextComponentReference.GetComponent<Renderer>();
        //            var (lineNumber, columnNumber) = GetLineAndColumnNumberFromString(cursor, cursor.Text);

        //            var horizontalLetterOffset = columnNumber * Camera.LetterWidth;
        //            var verticalLetterOffset = lineNumber * Camera.LetterHeight;

        //            //position = new PointF(textRenderer.CanvasRenderPosition.Left + horizontalLetterOffset, textRenderer.CanvasRenderPosition.Top + verticalLetterOffset);
        //            position = new PointF(horizontalLetterOffset, verticalLetterOffset);
        //            size = new(2, Camera.LetterHeight);
        //            renderer.CanvasRenderPosition = new(position, size);
        //            renderer.ZIndex = 1;

        //            //renderer.RenderingBehaviour = new CursorRenderer(textRenderer, Input.IsCommandExecutable);

        //            continue;
        //        }

        //    }

        //    // Si on a dépassé la largeur du canvas, on fait un retour à la ligne
        //    // Ce n'est pas une bonne façon de faire, car on dépasse déjà la largeur 
        //    // TODO
        //    //if (horizontalOffset > transform.Size.X)
        //    //{
        //    //    horizontalOffset += letterHeight;
        //    //    verticalOffset = 0;
        //    //}

        //    horizontalOffset = Transform.Position.X;
        //    verticalOffset += Camera.LetterHeight;

        //    // On a dépassé le canvas, on arrête
        //    if (verticalOffset > Transform.Size.Y)
        //    {
        //        break;
        //    }

        //}
    }

    public void DrawBackgroundAroundLine(Graphics gfx/*, float canvasWidth, float canvasHeight*/)
    {
        RectangleF boundingBox =
            Input.Lines
                 .SelectMany(s => s.LineSegments)
                 .Where(s => s.HasComponent<Renderer>())
                 .Select(r => r.GetComponent<UITransform>())
                 .Select(t => t.GetBoundingBox())
                 //.Append(new RectangleF(new PointF(0, canvasHeight), new SizeF(canvasWidth, 0))) // TODO Keep ?
                 .Aggregate(RectangleF.Union);

        gfx.FillRectangle(_inputBackgroundBrush, boundingBox);
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

    public void Render(Graphics gfx, Renderer renderer, RectangleF bounds)
    {
        // Draw the background around the input panel
        DrawBackgroundAroundLine(gfx);
    }

}
