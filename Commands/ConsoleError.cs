using System;

namespace CommandLineReimagine.Commands
{
    public class ConsoleError : Exception
    {
        public ConsoleError(string message) : base(message)
        {
            
        }
    }
}
