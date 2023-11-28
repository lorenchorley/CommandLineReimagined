using System.Drawing;
using Rendering.Components;
using EntityComponentSystem;

namespace UIComponents.Components
{
    public class InteractiveComponent : Component
    {
        [State]
        public virtual RectangleF Bounds { get; set; }

        public override void OnInit()
        {
            EnsureDependency<UITransform>();
        }
    }
}
