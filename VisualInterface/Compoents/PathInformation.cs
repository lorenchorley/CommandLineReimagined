﻿using EntityComponentSystem;

namespace Console.Components
{
    public class PathInformation : Component
    {
        [State] public string Path { get; set; }

        public override IEnumerable<(string, string)> SerialisableDebugProperties
        {
            get
            {
                yield return ("Path", Path);
            }
        }

    }
}
