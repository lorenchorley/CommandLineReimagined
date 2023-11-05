using Rendering.Components;
using EntityComponentSystem;

namespace Console.Components;

public class ConsoleInput : Component
{
    public List<LineComponent> PromptLines { get; set; } = new();
    public int SelectionStart { get; set; }
    public int SelectionLength { get; set; }
    public bool IsCommandExecutable { get; set; }

    public override IEnumerable<(string, string)> SerialisableDebugProperties
    {
        get
        {
            foreach (var activeLine in PromptLines)
            {
                yield return ("ActiveLine", $"{activeLine?.Id} ({activeLine?.ToText()})");
            }
            yield return ("SelectionStart", SelectionStart.ToString());
            yield return ("SelectionLength", SelectionLength.ToString());
        }
    }

    protected override void InsureDependencies()
    {
        Entity.TryAddComponent<Renderer>();
    }
}
