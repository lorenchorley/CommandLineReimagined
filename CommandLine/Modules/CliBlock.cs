using UIComponents;
using UIComponents.Components;
using EntityComponentSystem;

namespace CommandLine.Modules
{
    public class CliBlock
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConsoleLayout _consoleRenderer;
        private readonly ECS _ecs;

        public string Description { get; set; }
        public List<LineComponent> Lines { get; set; } = new();

        public CliBlock(IServiceProvider serviceProvider, ConsoleLayout consoleRenderer, ECS ecs)
        {
            _serviceProvider = serviceProvider;
            _consoleRenderer = consoleRenderer;
            _ecs = ecs;
        }

        public CliBlock SetDesciption(string description)
        {
            Description = description;
            return this;
        }

        public LineComponent NewLine()
        {
            LineComponent line = _ecs.NewEntity("Scoped : " + Description).AddComponent<LineComponent>();
            _consoleRenderer.Output.Lines.Add(line); // TODO Make this operation async safe
            Lines.Add(line);
            return line;
        }

        //public void Finalise()
        //{
        //    for (int i = 0; i < Lines.Count; i++)
        //    {
        //        _consoleRenderer.Output.Lines.Add(Lines[i]);
        //    }
        //}

        public void AbondonLine(LineComponent line)
        {
            _consoleRenderer.Output.Lines.Remove(line);
            Lines.Remove(line);
            line.Entity.Destroy();
        }

        internal void Clear()
        {
            foreach (var line in Lines)
            {
                _consoleRenderer.Output.Lines.Remove(line);
                line.Entity.Destroy();
            }

            Lines.Clear();
        }
    }
}
