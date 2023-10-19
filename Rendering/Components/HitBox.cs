using System.Drawing;
using CommandLineReimagine.Console.Components;
using EntityComponentSystem;

namespace CommandLineReimagine.Console.Interaction
{
    public class HitBox : Component
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
