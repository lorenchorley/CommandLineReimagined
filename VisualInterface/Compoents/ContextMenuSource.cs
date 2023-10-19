using CommandLineReimagine.Console.Interaction;
using EntityComponentSystem;

namespace CommandLineReimagine.Console.Components
{
    public class ContextMenuSource : Component
    {
        public string ContextMenuName { get; set; }

        public override IEnumerable<(string, string)> SerialisableDebugProperties
        {
            get
            {
                yield return ("ContextMenuName", ContextMenuName.ToString());
            }
        }

        override protected void InsureDependencies()
        {
            Entity.TryAddComponent<HitBox>();
        }
    }
}
