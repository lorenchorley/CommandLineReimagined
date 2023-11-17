using EntityComponentSystem;

namespace UIComponents.Components;

public class PathInformation : Component
{
    [State]
    public virtual string Path { get; set; }
}
