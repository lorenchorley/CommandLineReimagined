using Rendering.Components;
using EntityComponentSystem;

namespace Console.Components;

public class ConsoleOutput : Component
{
    public List<LineComponent> Lines { get; private set; } = new();

    public override IEnumerable<(string, string)> SerialisableDebugProperties
    {
        get
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                var line = Lines[i];
                var firstCoordinates
                    = line.GetOrderedLineSegments()
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

    protected override void InsureDependencies()
    {
        Entity.TryAddComponent<Renderer>();
    }

}
