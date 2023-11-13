using Rendering.Interaction;
using EntityComponentSystem;

namespace UIComponents.Components
{
    public class DoubleClickAction : Component
    {
        [State] public string ActionName { get; set; } = "";

        public override IEnumerable<(string, string)> SerialisableDebugProperties
        {
            get
            {
                yield return ("ActionName", ActionName);
            }
        }

        public override void OnInit()
        {
            EnsureDependency<InteractiveComponent>();
        }
    }
}
