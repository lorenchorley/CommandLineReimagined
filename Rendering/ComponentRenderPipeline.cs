using EntityComponentSystem;
using EntityComponentSystem.Attributes;
using Rendering.Components;
using Rendering.Events;
using Rendering.Spaces;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;

namespace Rendering;

public class ComponentRenderPipeline
{
    private bool _debugRendering = false;

    private Pen _pen = new Pen(Color.White);
    private Pen _cursorPen = new Pen(Color.White);
    private Pen _buttonPen = new Pen(new SolidBrush(Color.WhiteSmoke));
    private Pen _debugPen = new Pen(new HatchBrush(HatchStyle.BackwardDiagonal, Color.White));
    private Brush _inputBackgroundBrush = new SolidBrush(Color.RoyalBlue);
    private Font _font = new Font(FontFamily.GenericMonospace, 14);

    private const float marginBottomBidouille = 12; // Bidouille pour éviter que le texte ne soit coupé en bas du canvas
    private float _leftMargin = 5;
    private float _rightMargin = 5;
    private float _bottomMargin = 5 + marginBottomBidouille;
    private float _topMargin = 5;

    private bool _needsCalculationRefresh = true;
    private float _letterWidth;
    private float _letterHeight;

    private readonly ECS _ecs;
    private readonly PhysicalScreenSpace _physicalScreenSpace;

    public ComponentRenderPipeline(ECS ecs, PhysicalScreenSpace physicalScreenSpace)
    {
        _ecs = ecs;
        _physicalScreenSpace = physicalScreenSpace;
    }

    /// <summary>
    /// Utilisation des composant ConsoleInput et ConsoleOutput pour faire les deux parties du rendu
    /// </summary>
    /// <param name="gfx"></param>
    /// <param name="canvasWidth"></param>
    /// <param name="canvasHeight"></param>
    public void Draw(Graphics gfx, float canvasWidth, float canvasHeight, ECS.ShadowECS shadowECS)
    {
        _ecs.RegisterEvent(new RenderEvent());

        // TODO Positionner les transforms de Input et Output selon les bidouilles
        // Ajouter la marge à ce niveau
        //UITransform outputTransform = Output.Entity.GetComponent<UITransform>();

        //outputTransform.Position = new Vector2(_leftMargin, _topMargin);
        //outputTransform.Size = new Vector2(canvasWidth - _leftMargin - _rightMargin, canvasHeight - _topMargin - _bottomMargin);


        // TODO Adjuster l'input pour predre en compte la quantité de lignes produites avant de faire la positionnement de l'output


        //List<UILayoutComponent> layouts =
        //    GetLayouts(shadowECS.Entities.ToList()).ToList();

        //// Première passe pour savoir quels élements devraient y être et où il faut les placer
        //foreach (var layout in layouts)
        //{
        //    layout.RecalculateChildTransforms();
        //}

        // Profil de tous les éléments à dessiner
        List<Renderer> componentsToRender =
            shadowECS.Components
                     .OfType<Renderer>()
                     .ToList();

        //componentsToRender.ForEach(r => r.IsVisible = false); // TODO Garder la dernière liste pour ne pas tout reparcourir à chaque fois

        // Deuxième passe pour dessiner les éléments
        RenderElements(gfx, componentsToRender);
    }

    private IEnumerable<UILayoutComponent> GetLayouts(IEnumerable<Entity> entites)
    {
        // Search for all uilayoutcomponents and recursively return them from the lowest layer first
        foreach (var entity in entites)
        {
            foreach (var layout in GetLayouts(entity.Children.ToArray()))
            {
                yield return layout;
            }

            if (entity.TryGetComponent(out UILayoutComponent? uILayoutComponent))
            {
                yield return uILayoutComponent;
            }
        }
    }

    private void RenderElements(Graphics gfx, List<Renderer> elementsToRender)
    {
        //var renderers = elementsToRender.Where(r => r.IsVisible).ToList();

        // Sort by ZIndex
        elementsToRender.Sort();

        foreach (var renderer in elementsToRender)
        {
            //RectangleF bounds = renderer.CanvasRenderPosition;

            renderer.UITransform ??= renderer.Entity.GetOrAddComponent<UITransform>();
            RectangleF bounds = renderer.UITransform.BoundsToScreenSpace(_physicalScreenSpace);
            renderer.CanvasRenderPosition = bounds;

            //if (_debugRendering)
            //    gfx.DrawRectangle(_debugPen, bounds);

            renderer.RenderingBehaviour?.Render(gfx, renderer, bounds);
        }
    }

}
