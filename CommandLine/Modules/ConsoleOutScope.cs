using CommandLineReimagined.Console;
using CommandLineReimagined.Console.Components;
using EntityComponentSystem;

namespace CommandLine.Modules
{
    public class ConsoleOutScope
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConsoleLayout _consoleRenderer;
        private readonly ECS _ecs;

        public string Description { get; set; }
        public List<Line> Lines { get; set; } = new();

        public ConsoleOutScope(IServiceProvider serviceProvider, ConsoleLayout consoleRenderer, ECS ecs)
        {
            _serviceProvider = serviceProvider;
            _consoleRenderer = consoleRenderer;
            _ecs = ecs;
        }

        public ConsoleOutScope SetDesciption(string description)
        {
            Description = description;
            return this;
        }

        public Line NewLine()
        {
            Line line = _ecs.NewEntity("Scoped : " + Description).AddComponent<Line>();
            Lines.Add(line);
            return line;
        }

        public void Finalise()
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                _consoleRenderer.Output.Lines.Add(Lines[i]);
            }
        }
    }
}
