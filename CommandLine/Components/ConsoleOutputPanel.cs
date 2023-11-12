using Console.Components;
using EntityComponentSystem;
using Rendering.Components;

namespace UIComponents.Compoents.Console;

public class ConsoleOutputPanel : UILayoutComponent
{
    [State] public List<LineComponent> Lines { get; private set; } = new();

    public override IEnumerable<(string, string)> SerialisableDebugProperties
    {
        get
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                var line = Lines[i];
                var firstCoordinates
                    = line.LineSegments
                          .OfType<Component>()
                          .FirstOrDefault()
                          ?.Entity
                          ?.GetComponent<Renderer>()
                          ?.CanvasRenderPosition
                          .ToString();
                yield return ($"Line[{i}]", $"{line.Id} ({firstCoordinates}, {line.ToText()})");
            }
        }
    }

    public override void OnInit()
    {
        EnsureDependency<Renderer>();
    }

    public override void RecalculateChildTransforms()
    {
        throw new NotImplementedException();
    }
}
