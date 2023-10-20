using CommandLineReimagined.Rendering.Components;
using CommandLineReimagined.Rendering.Interaction;
using EntityComponentSystem;

namespace CommandLineReimagined.Console.Components
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
