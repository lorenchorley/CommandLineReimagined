using Rendering.Interaction;
using EntityComponentSystem;

namespace UIComponents.Components
{
    public class ContextMenuSource : Component
    {
        [State] public string ContextMenuName { get; set; }

        public override void OnInit()
        { 
            EnsureDependency<InteractiveComponent>();
        }
    }
}
