using Rendering.Interaction;
using EntityComponentSystem;

namespace Console.Components
{
    public class DoubleClickAction : Component
    {
        public string ActionName { get; set; } = "";

        public override IEnumerable<(string, string)> SerialisableDebugProperties
        {
            get
            {
                yield return ("ActionName", ActionName);
            }
        }

        override protected void InsureDependencies()
        {
            Entity.TryAddComponent<InteractiveComponent>();
        }
    }
}
