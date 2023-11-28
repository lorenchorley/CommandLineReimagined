using EntityComponentSystem;

namespace UIComponents.Components
{
    public class DoubleClickAction : Component
    {
        [State]
        public virtual string ActionName { get; set; } = "";

        public override void OnInit()
        {
            EnsureDependency<InteractiveComponent>();
        }
    }
}
