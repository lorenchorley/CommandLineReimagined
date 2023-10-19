using System;

namespace CommandLineReimagine.Console.Interaction
{
    [Flags]
    public enum InteractableElementLayer
    {
        Navigation = 0,
        Text = 1 << 0,
        ActiveTextBlock = 1 << 1,
        Button = 1 << 2
    }
}
