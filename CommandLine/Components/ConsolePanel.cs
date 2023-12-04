using UIComponents.Components;
using EntityComponentSystem;
using Rendering.Components;
using System.Drawing;

namespace UIComponents.Compoents.Console;

public class ConsolePanel : UILayoutComponent
{
    [State] public virtual float Height { get; set; } = 0;
    [State] public virtual List<LineComponent> Lines { get; set; } = new();

    public override void OnInit()
    {
        EnsureDependency<Renderer>();
    }

    public override void RecalculateChildTransforms()
    {
        Height = 0;

        foreach (var line in Lines)
        {
            foreach (var lineSegment in line.LineSegments)
            {
                //Height += lineSegment.Height; // How to get the line height at this stage in UI space ?

                Renderer renderer = lineSegment.Entity.GetComponent<Renderer>();
                renderer.IsVisible = true; // Marquer comme visible pour le rendu, toujours nécessaire ?

                if (lineSegment is TextComponent textBlock)
                {

                    continue;
                }

                if (lineSegment is HighlightComponent highlight)
                {

                    continue;
                }

                if (lineSegment is ButtonComponent button)
                {

                    continue;
                }

                if (lineSegment is CursorComponent cursor)
                {

                    continue;
                }
            }
        }
    }

}
