using Console.Components;
using EntityComponentSystem;
using Rendering.Components;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;

namespace Console;

public class ConsoleLayout
{
    private bool _debugRendering = false;

    private Pen _pen = new Pen(Color.White);
    private Pen _cursorPen = new Pen(Color.White);
    private Pen _buttonPen = new Pen(new SolidBrush(Color.WhiteSmoke));
    private Pen _debugPen = new Pen(new HatchBrush(HatchStyle.BackwardDiagonal, Color.White));
    private Brush _inputBackgroundBrush = new SolidBrush(Color.RoyalBlue);
    private Font _font = new Font(FontFamily.GenericMonospace, 14);

    public ConsoleDetails Details { get; private init; }
    public ConsoleOutput Output { get; private init; }
    public ConsoleInput Input { get; private init; }

    //private int _lineHeight = 20;
    private const float marginBottomBidouille = 12; // Bidouille pour éviter que le texte ne soit coupé en bas du canvas
    private float _leftMargin = 5;
    private float _rightMargin = 5;
    private float _bottomMargin = 5 + marginBottomBidouille;
    private float _topMargin = 5;
    private readonly ECS _ecs;

    public ConsoleLayout(ECS ecs)
    {
        _ecs = ecs;

        Details = _ecs.NewEntity("Debug Details").AddComponent<ConsoleDetails>();

        Output = _ecs.NewEntity("Output").AddComponent<ConsoleOutput>();

        Input = _ecs.NewEntity("Input").AddComponent<ConsoleInput>();
        //Input.ActiveLines.Add(_ecs.NewEntity("Active line").AddComponent<LineComponent>());
    }

    public LineComponent NewLine(string description)
    {
        Entity lineObject = _ecs.NewEntity(description);
        LineComponent line = lineObject.AddComponent<LineComponent>();
        Output.Lines.Add(line);
        return line;
    }

    private bool _needsCalculationRefresh = true;
    private float _letterWidth;
    private float _letterHeight;

    /// <summary>
    /// Utilisation des composant ConsoleInput et ConsoleOutput pour faire les deux parties du rendu
    /// </summary>
    /// <param name="gfx"></param>
    /// <param name="canvasWidth"></param>
    /// <param name="canvasHeight"></param>
    public void Draw(Graphics gfx, float canvasWidth, float canvasHeight)
    {
        Details.ConsoleSize = new Vector2(canvasWidth, canvasHeight);

        float bidouilleHorizontalRatio = 0.655f;
        float bidouilleVerticalRatio = 0.85f;

        if (_needsCalculationRefresh)
        {
            SizeF letterSize = gfx.MeasureString("w", _font);
            _letterWidth = letterSize.Width * bidouilleHorizontalRatio;
            _letterHeight = letterSize.Height * bidouilleVerticalRatio;

            Details.LetterSize = new Vector2(_letterWidth, _letterHeight);

            _needsCalculationRefresh = false;
        }

        // TODO Positionner les transforms de Input et Output selon les bidouilles
        // Ajouter la marge à ce niveau
        UITransform outputTransform = Output.Entity.GetComponent<UITransform>();

        outputTransform.Position = new Vector2(_leftMargin, _topMargin);
        outputTransform.Size = new Vector2(canvasWidth - _leftMargin - _rightMargin, canvasHeight - _topMargin - _bottomMargin);


        // TODO Adjuster l'input pour predre en compte la quantité de lignes produites avant de faire la positionnement de l'output

        // Profil de tous les éléments à dessiner
        List<Renderer> componentsToRender =
            _ecs.AccessEntities(list => 
                list.OfType<Entity>()
                    .Choose(e => e.TryGetComponent<Renderer>())
                    .ToList()
            );

        componentsToRender.ForEach(r => r.IsVisible = false); // TODO Garder la dernière liste pour ne pas tout reparcourir à chaque fois

        // Remplir l'espace restant avec les lignes précédentes
        // ====================================================

        // Première passe pour savoir quels élements devraient y être et où il faut les placer
        RecalculateChildTransforms(GetLines().ToList(), outputTransform);

        // Dessiner les éléments visuels correspondant à l'input
        DrawBackgroundAroundLine(gfx, Input.PromptLines, canvasWidth, canvasHeight);

        // Deuxième passe pour dessiner les éléments
        RenderElements(gfx, componentsToRender);
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

    private void RecalculateChildTransforms(List<LineComponent> lines, UITransform transform)
    {
        PointF position;
        float calculatedWidth;
        SizeF size;

        float verticalOffset = transform.Position.Y;
        float horizontalOffset = transform.Position.X; // TODO cette valeur n'est pas exploitée pour faire des retours à la ligne

        PointF NewPosition(float x, float y) => new PointF(x, transform.Size.Y - y);

        for (int i = 0; i < lines.Count; i++)
        {
            LineComponent line = lines[i];

            foreach (ILineSegment lineSegment in line.GetOrderedLineSegments())
            {
                Renderer renderer = ((Component)lineSegment).Entity.GetComponent<Renderer>();
                renderer.IsVisible = true; // Marquer comme visible pour le rendu

                //renderer.Positioner?.Position();

                if (lineSegment is TextComponent textBlock)
                {
                    // TODO Gérer les retours à la ligne
                    var newLineCount = textBlock.Text.Where(c => c == '\n').Count();
                    verticalOffset += _letterHeight * newLineCount; // Ajouter de l'hauteur s'il y a des retours à la ligne

                    position = NewPosition(horizontalOffset, verticalOffset);
                    calculatedWidth = _letterWidth * textBlock.Text.Length;
                    size = new(calculatedWidth, _letterHeight * (newLineCount + 1));
                    renderer.CanvasRenderPosition = new(position, size);
                    horizontalOffset += size.Width;

                    renderer.RenderingBehaviour = new TextRenderer(textBlock.Text, textBlock.Highlighted);

                    continue;
                }

                if (lineSegment is HighlightComponent highlight)
                {
                    // Positionner le rectangle de highlight afin que ça correspond à Line et Column du texte
                    var textRenderer = highlight.TextComponent.GetComponent<Renderer>();

                    var horizontalLetterOffset = highlight.Column * _letterWidth;
                    var verticalLetterOffset = highlight.Line * _letterHeight;

                    var trailingLineLength = GetTrailingLineLength(highlight.TextComponent.Text, highlight.Line, highlight.Column);

                    //position = new PointF(textRenderer.CanvasRenderPosition.Left + horizontalLetterOffset, textRenderer.CanvasRenderPosition.Top + verticalLetterOffset);
                    position = new PointF(horizontalLetterOffset, verticalLetterOffset);
                    size = new(_letterWidth, _letterHeight);
                    renderer.CanvasRenderPosition = new(position, size);
                    renderer.ZIndex = -1;

                    renderer.RenderingBehaviour = new HighlightRenderer(textRenderer, trailingLineLength, _letterWidth, _letterHeight);

                    continue;
                }

                if (lineSegment is ButtonComponent button)
                {
                    // TODO Gérer les retours à la ligne
                    horizontalOffset += _letterWidth;

                    position = NewPosition(horizontalOffset, verticalOffset);
                    calculatedWidth = _letterWidth * button.Text.Length;
                    size = new(calculatedWidth, _letterHeight);
                    renderer.CanvasRenderPosition = new(position, size);

                    horizontalOffset += _letterWidth;
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

                    var horizontalLetterOffset = columnNumber * _letterWidth;
                    var verticalLetterOffset = lineNumber * _letterHeight;

                    //position = new PointF(textRenderer.CanvasRenderPosition.Left + horizontalLetterOffset, textRenderer.CanvasRenderPosition.Top + verticalLetterOffset);
                    position = new PointF(horizontalLetterOffset, verticalLetterOffset);
                    size = new(2, _letterHeight);
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

            horizontalOffset = transform.Position.X;
            verticalOffset += _letterHeight;

            // On a dépassé le canvas, on arrête
            if (verticalOffset > transform.Size.Y)
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

    private void DrawBackgroundAroundLine(Graphics gfx, List<LineComponent> lines, float canvasWidth, float canvasHeight)
    {
        RectangleF boundingBox =
            lines.SelectMany(s => s.GetOrderedLineSegments())
                 .OfType<TextComponent>()
                 .Select(s => s.GetComponent<Renderer>())
                 .Select(r => r.CanvasRenderPosition)
                 .Append(new RectangleF(new PointF(0, canvasHeight), new SizeF(canvasWidth, 0)))
                 .Aggregate((r, s) => RectangleF.Union(r, s));

        gfx.FillRectangle(_inputBackgroundBrush, boundingBox);
    }

    private void RenderElements(Graphics gfx, List<Renderer> elementsToRender)
    {
        var renderers = elementsToRender.Where(r => r.IsVisible).ToList();

        // Sort by ZIndex
        renderers.Sort();

        foreach (var renderer in renderers)
        {
            RectangleF bounds = renderer.CanvasRenderPosition;

            if (_debugRendering)
                gfx.DrawRectangle(_debugPen, bounds);


            renderer.RenderingBehaviour?.Render(gfx, renderer, bounds);

        }
    }

}
