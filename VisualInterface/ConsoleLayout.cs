using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Numerics;
using CommandLineReimagine.Console.Components;
using EntityComponentSystem;

namespace CommandLineReimagine.Console
{
    public class ConsoleLayout
    {
        private bool _debugRendering = false;

        private Pen _pen = new Pen(Color.White);
        private Pen _cursorPen = new Pen(Color.White);
        private Pen _buttonPen = new Pen(new SolidBrush(Color.WhiteSmoke));
        private Pen _debugPen = new Pen(new HatchBrush(HatchStyle.BackwardDiagonal, Color.White));
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
            Input.ActiveLine = _ecs.NewEntity("Active line").AddComponent<Line>();
        }

        public Line NewLine(string description)
        {
            Entity lineObject = _ecs.NewEntity(description);
            Line line = lineObject.AddComponent<Line>();
            Output.Lines.Add(line);
            return line;
        }

        private bool _needsCalculationRefresh = true;
        float letterWidth;
        float letterHeight;

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
                letterWidth = letterSize.Width * bidouilleHorizontalRatio;
                letterHeight = letterSize.Height * bidouilleVerticalRatio;

                Details.LetterSize = new Vector2(letterWidth, letterHeight);

                _needsCalculationRefresh = false;
            }

            // TODO Positionner les transforms de Input et Output selon les bidouilles
            // Ajouter la marge à ce niveau
            UITransform outputTransform = Output.Entity.GetComponent<UITransform>();
            //UITransform inputTransform = Input.Entity.GetComponent<UITransform>();

            //inputTransform.Size = new Vector2(canvasWidth - _leftMargin - _rightMargin, letterHeight); // TODO la hauteur pourrait être une multiple de letterHeight s'il y a un retour à la ligne

            // Détermine la ligne horizontale où le Output s'arrête et le Input commence
            //float separatorHeight = canvasHeight - marginBottomBidouille - _bottomMargin - inputTransform.Size.Y;

            //inputTransform.Position = new Vector2(_leftMargin, separatorHeight);

            // Le output remplit ce qui reste
            //outputTransform.Position = new Vector2(_leftMargin, _topMargin);
            //outputTransform.Size = new Vector2(canvasWidth, separatorHeight - _topMargin);

            outputTransform.Position = new Vector2(_leftMargin, _topMargin);
            outputTransform.Size = new Vector2(canvasWidth - _leftMargin - _rightMargin, canvasHeight - _topMargin - _bottomMargin);


            // TODO Adjuster l'input pour predre en compte la quantité de lignes produites avant de faire la positionnement de l'output

            //float verticalOffset = _verticalMargin;
            //float horizontalOffset = _horizontalMargin;


            // Profil de tous les éléments à dessiner
            List<Renderer> componentsToRender =
                _ecs.RegisteredEntities
                    .OfType<Entity>()
                    .Choose(e => e.TryGetComponent<Renderer>())
                    .ToList();

            componentsToRender.ForEach(r => r.IsVisible = false); // TODO Garder la dernière liste pour ne pas tout reparcourir à chaque fois

            // On va actualiser la liste des hitboxes
            // On va faire un rendu de tous les composants, plus tard peut-être on fait une optimisation pour que ceux qui sont visibles
            //_ecs.Clear();

            // Ligne active
            // ============
            //var activeLine = Input.ActiveLine;

            //PointF position = NewPosition(horizontalOffset, verticalOffset);
            //float calculatedWidth = letterWidth * activeRenderableTextBlock.Text.Length;
            //SizeF size = new SizeF(calculatedWidth, letterHeight);
            //RectangleF bounds = new RectangleF(position, size);

            //componentsToRender.Add((activeRenderableTextBlock, bounds));

            //verticalOffset += letterHeight;

            //float cursorPosition = input


            // Remplir l'espace restant avec les lignes précédentes
            // ====================================================

            // Première passe pour savoir quels élements devraient y être et où il faut les placer
            //RecalculateChildTransforms(new() { Input.ActiveLine }, inputTransform);
            //RecalculateChildTransforms(Output.Lines, outputTransform);
            RecalculateChildTransforms(GetLines().ToList(), outputTransform);


            // Deuxième passe pour dessiner les éléments
            DrawElements(gfx, componentsToRender);
        }

        private IEnumerable<Line> GetLines()
        {
            yield return (Line)Input.ActiveLine;

            //foreach (var line in Output.Lines)
            for (int i = Output.Lines.Count - 1; i >= 0; i--)
            {
                yield return Output.Lines[i];
            }
        }

        private void RecalculateChildTransforms(List<Line> lines, UITransform transform)
        {
            PointF position;
            float calculatedWidth;
            SizeF size;

            float verticalOffset = transform.Position.Y;
            float horizontalOffset = transform.Position.X; // TODO cette valeur n'est pas exploitée pour faire des retours à la ligne

            PointF NewPosition(float x, float y) => new PointF(x, transform.Size.Y - y);

            for (int i = 0; i < lines.Count; i++)
            {
                Line line = lines[i];

                foreach (ILineSegment lineSegment in line.GetOrderedLineSegments())
                {
                    Renderer renderer = ((Component)lineSegment).Entity.GetComponent<Renderer>();
                    renderer.IsVisible = true; // Marquer comme visible pour le rendu

                    if (lineSegment is TextBlock textBlock)
                    {
                        // TODO Gérer les retours à la ligne

                        position = NewPosition(horizontalOffset, verticalOffset);
                        calculatedWidth = letterWidth * textBlock.Text.Length;
                        size = new(calculatedWidth, letterHeight);

                        renderer.CanvasRenderPosition = new(position, size);

                        horizontalOffset += size.Width;

                        continue;
                    }

                    if (lineSegment is Button button)
                    {
                        // TODO Gérer les retours à la ligne
                        horizontalOffset += letterWidth;

                        position = NewPosition(horizontalOffset, verticalOffset);
                        calculatedWidth = letterWidth * button.Text.Length;
                        size = new(calculatedWidth, letterHeight);
                        renderer.CanvasRenderPosition = new(position, size);

                        horizontalOffset += letterWidth;
                        horizontalOffset += size.Width;

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
                verticalOffset += letterHeight;

                // On a dépassé le canvas, on arrête
                if (verticalOffset > transform.Size.Y)
                {
                    break;
                }

            }
        }

        // TODO Cette méthode pourrait sortir dans sa propre classe de rendu, il s'agit d'une phase à part
        private void DrawElements(Graphics gfx, List<Renderer> elementsToRender)
        {
            foreach (var renderer in elementsToRender.Where(r => r.IsVisible))
            {
                var bounds = renderer.CanvasRenderPosition;

                if (_debugRendering)
                    gfx.DrawRectangle(_debugPen, bounds);

                if (renderer.Entity.TryGetComponent(out TextBlock? textBlock))
                {
                    // TODO Gérer les retours à la ligne+

                    gfx.DrawString(textBlock.Text, _font, Brushes.White, bounds.Location);

                }
                //else if (renderer.Entity.TryGetComponent(out NewLine? newLine))
                //{
                //    if (_debugRendering)
                //        gfx.DrawLine(_debugPen, bounds.Location, new PointF(bounds.Location.X, bounds.Location.Y + bounds.Height));
                //}
                else if (renderer.Entity.TryGetComponent(out Button? button))
                {
                    // TODO Gérer les retours à la ligne

                    gfx.DrawRectangle(_buttonPen, bounds);
                    gfx.DrawString(button.Text, _font, Brushes.White, bounds.Location);

                }


            }
        }

    }
}
