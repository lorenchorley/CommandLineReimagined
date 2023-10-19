using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandLineReimagine.Configuration
{
    public class RenderingOptions
    {
        public const string ConfigurationSection = "RenderingOptions";
        public int FrameRate { get; set; }
    }
}
 