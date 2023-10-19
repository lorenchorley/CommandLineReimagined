using EntityComponentSystem;

namespace CommandLineReimagine.Console.Components;

public class ConsoleInput : Component
{
    public Line? ActiveLine { get; set; }
    public int SelectionStart { get; set; }
    public int SelectionLength { get; set; }

    public override IEnumerable<(string, string)> SerialisableDebugProperties
    {
        get
        {
            yield return ("ActiveLine", $"{ActiveLine?.Id} ({ActiveLine?.ToText()})");
            yield return ("SelectionStart", SelectionStart.ToString());
            yield return ("SelectionLength", SelectionLength.ToString());
        }
    }

    protected override void InsureDependencies()
    {
        Entity.TryAddComponent<Renderer>();
    }
}
