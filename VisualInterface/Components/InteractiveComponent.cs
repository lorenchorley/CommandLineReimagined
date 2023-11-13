using System.Drawing;
using Rendering.Components;
using EntityComponentSystem;

namespace Rendering.Interaction
{
    public class InteractiveComponent : Component
    {
        [State] public RectangleF Bounds { get; init; }

        public override IEnumerable<(string, string)> SerialisableDebugProperties
        {
            get
            {
                yield return ("Bounds", Bounds.ToString());
            }
        }

        public override void OnInit()
        {
            EnsureDependency<UITransform>();
        }
    }
}
