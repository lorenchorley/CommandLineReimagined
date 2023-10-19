using CommandLineReimagined.Console.Entities;
using System.Collections.Generic;
using System.Windows.Shapes;
using CommandLineReimagine.Console.Interaction;

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
