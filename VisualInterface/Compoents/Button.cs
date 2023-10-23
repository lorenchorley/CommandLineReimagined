using Rendering.Components;
using Rendering.Interaction;
using EntityComponentSystem;

namespace Console.Components
{
    public class Button : Component, ILineSegment
    {
        public string Text { get; set; }

        public override IEnumerable<(string, string)> SerialisableDebugProperties
        {
            get
            {
                yield return ("Text", Text);
            }
        }

        public string ToText()
        {
            return Text;
        }

        protected override void InsureDependencies()
        {
            Entity.TryAddComponent<Renderer>();
            Entity.TryAddComponent<InteractiveComponent>();
        }

    }
}
