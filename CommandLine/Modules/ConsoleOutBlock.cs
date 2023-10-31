using Console;
using Console.Components;
using EntityComponentSystem;

namespace CommandLine.Modules
{
    public class ConsoleOutBlock
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConsoleLayout _consoleRenderer;
        private readonly ECS _ecs;

        public string Description { get; set; }
        public List<LineComponent> Lines { get; set; } = new();

        public ConsoleOutBlock(IServiceProvider serviceProvider, ConsoleLayout consoleRenderer, ECS ecs)
        {
            _serviceProvider = serviceProvider;
            _consoleRenderer = consoleRenderer;
            _ecs = ecs;
        }

        public ConsoleOutBlock SetDesciption(string description)
        {
            Description = description;
            return this;
        }

        public LineComponent NewLine()
        {
            LineComponent line = _ecs.NewEntity("Scoped : " + Description).AddComponent<LineComponent>();
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

        public void AbondonLine(LineComponent line)
        {
            Lines.Remove(line);
        }
    }
}
