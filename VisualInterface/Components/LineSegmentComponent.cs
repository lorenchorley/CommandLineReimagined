using EntityComponentSystem;
using Rendering.Components;

namespace UIComponents.Components
{
    public abstract class LineSegmentComponent : Component
    {
        public override void OnInit()
        {
            EnsureDependency<Renderer>();
        }

        public abstract string ToText();
    }
}