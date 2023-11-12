using Rendering.Interaction;
using EntityComponentSystem;

namespace Console.Components
{
    public class ContextMenuSource : Component
    {
        [State] public string ContextMenuName { get; set; }

        public override IEnumerable<(string, string)> SerialisableDebugProperties
        {
            get
            {
                yield return ("ContextMenuName", ContextMenuName.ToString());
            }
        }

        public override void OnInit()
        { 
            EnsureDependency<InteractiveComponent>();
        }
    }
}
