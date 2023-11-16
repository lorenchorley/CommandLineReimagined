using EntityComponentSystem;

namespace UIComponents.Components
{
    public class PathInformation : Component
    {
        [State] public string Path { get; set; }
    }
}
