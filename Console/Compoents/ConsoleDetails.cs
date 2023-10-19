using CommandLineReimagined.Console.Entities;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Documents;
using static System.Net.Mime.MediaTypeNames;

namespace CommandLineReimagine.Console.Components;

public class ConsoleDetails : Component
{
    public Vector2 ConsoleSize { get; set; }
    public Vector2 LetterSize { get; set; }

    public override IEnumerable<(string, string)> SerialisableDebugProperties
    {
        get
        {
            yield return ("ConsoleSize", ConsoleSize.ToString());
            yield return ("LetterSize", LetterSize.ToString());
        }
    }

}
