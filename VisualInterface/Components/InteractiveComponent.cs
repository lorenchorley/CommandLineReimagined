using System.Drawing;
using Rendering.Components;
using EntityComponentSystem;

namespace Rendering.Interaction
{
    public class InteractiveComponent : Component
    {
        [State] public RectangleF Bounds { get; init; }

        public override void OnInit()
        {
            EnsureDependency<UITransform>();
        }
    }
}
