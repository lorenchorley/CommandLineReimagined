using EntityComponentSystem;

namespace CommandLineReimagine.Console.Components
{
    public class PathInformation : Component
    {
        public string Path { get; set; }

        public override IEnumerable<(string, string)> SerialisableDebugProperties
        {
            get
            {
                yield return ("Path", Path);
            }
        }

    }
}
