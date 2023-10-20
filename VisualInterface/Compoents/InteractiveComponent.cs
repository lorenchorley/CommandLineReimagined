using System.Drawing;
using CommandLineReimagined.Rendering.Components;
using EntityComponentSystem;

namespace CommandLineReimagined.Rendering.Interaction
{
    public class InteractiveComponent : Component
    {
        public RectangleF Bounds { get; init; }

        public override IEnumerable<(string, string)> SerialisableDebugProperties
        {
            get
            {
                yield return ("Bounds", Bounds.ToString());
            }
        }

        override protected void InsureDependencies()
        {
            Entity.TryAddComponent<UITransform>();
        }
    }
}
