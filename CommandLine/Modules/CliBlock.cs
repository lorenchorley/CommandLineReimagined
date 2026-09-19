using UIComponents;
using UIComponents.Components;
using EntityComponentSystem;
using Terminal.Execution;

namespace CommandLine.Modules
{
    /// <summary>
    /// One command's block of console output, as entities in the scene.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="ICommandOutput"/> so commands can write progress without
    /// depending on the ECS. That indirection is what lets the execution layer be tested
    /// headlessly, since a test can substitute a recorder for this.
    /// </remarks>
    public class CliBlock : ICommandOutput, IClearableOutput
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConsoleLayout _consoleRenderer;
        private readonly ECS _ecs;

        public string Description { get; set; } = string.Empty;
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

        public LineComponent NewLineComponent()
        {
            LineComponent line = _ecs.NewEntity("Scoped : " + Description).AddComponent<LineComponent>();
            Lines.Add(line);
            return line;
        }

        IOutputLine ICommandOutput.NewLine() => new BlockLine(NewLineComponent());

        void ICommandOutput.AbandonLine(IOutputLine line)
        {
            if (line is BlockLine blockLine)
            {
                AbondonLine(blockLine.Line);
            }
        }

        public void AbondonLine(LineComponent line)
        {
            Lines.Remove(line);
            line.Entity.Destroy();
        }

        public void Clear()
        {
            foreach (var line in Lines)
            {
                line.Entity.Destroy();
            }

            Lines.Clear();
        }

        private sealed class BlockLine : IOutputLine
        {
            public BlockLine(LineComponent line) => Line = line;

            public LineComponent Line { get; }

            public IOutputText Write(string description, string text) =>
                new BlockText(Line.LinkNewTextBlock(description, text));
        }

        private sealed class BlockText : IOutputText
        {
            private readonly TextComponent _component;

            public BlockText(TextComponent component) => _component = component;

            public string Text
            {
                get => _component.Text;
                set => _component.Text = value;
            }
        }
    }
}
