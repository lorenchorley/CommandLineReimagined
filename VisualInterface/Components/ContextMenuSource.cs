using EntityComponentSystem;

namespace UIComponents.Components
{
    public class ContextMenuSource : Component
    {
        [State]
        public virtual string ContextMenuName { get; set; } = null!;

        public override void OnInit()
        {
            base.OnInit();
            EnsureDependency<InteractiveComponent>();
        }
    }
}
