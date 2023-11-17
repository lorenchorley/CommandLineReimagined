using UIComponents.Components;
using EntityComponentSystem;
using Rendering.Components;

namespace UIComponents.Compoents.Console;

public class ConsoleOutputPanel : UILayoutComponent
{
    [State] public List<LineComponent> Lines { get; private set; } = new();

    public override void OnInit()
    {
        EnsureDependency<Renderer>();
    }

    public override void RecalculateChildTransforms()
    {
        throw new NotImplementedException();
    }
}
