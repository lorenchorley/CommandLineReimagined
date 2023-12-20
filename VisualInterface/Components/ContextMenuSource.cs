using EntityComponentSystem;

namespace UIComponents.Components
{
    public class ContextMenuSource : Component
    {
        [State]
        public virtual string ContextMenuName { get; set; }

        public override void OnInit()
        {
            base.OnInit();
            EnsureDependency<InteractiveComponent>();
        }
    }
}
