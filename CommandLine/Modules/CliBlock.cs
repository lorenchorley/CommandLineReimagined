using UIComponents;
using UIComponents.Components;
using CommandLineReimagined.Core;
using Controller;
using EntityComponentSystem;

namespace CommandLine.Modules
{
    /// <summary>
    /// One command's block of console output, as entities in the scene.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="IOutput"/> so commands can write progress without depending
    /// on the ECS. That indirection is what lets the core be tested headlessly, since a
    /// test can substitute a recorder for this.
    ///
    /// Asking the render loop for a frame is done here, on every write, rather than in
    /// each command. The commands used to inject <see cref="LoopController"/> just to call
    /// <c>RequestLoop</c> after each progress update, which made them impossible to
    /// construct anywhere without a render loop, such as the browser. The output sink is
    /// the one thing that knows whether a redraw is even a concept.
    /// </remarks>
    public class CliBlock : IOutput
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConsoleLayout _consoleRenderer;
        private readonly ECS _ecs;
        private readonly LoopController _loopController;

        public string Description { get; set; } = string.Empty;
        public List<LineComponent> Lines { get; set; } = new();

        public CliBlock(IServiceProvider serviceProvider, ConsoleLayout consoleRenderer, ECS ecs, LoopController loopController)
        {
            _serviceProvider = serviceProvider;
            _consoleRenderer = consoleRenderer;
            _ecs = ecs;
            _loopController = loopController;
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

        IOutputLine IOutput.NewLine()
        {
            var line = new BlockLine(NewLineComponent(), _loopController);
            _loopController.RequestLoop();
            return line;
        }

        public void AbondonLine(LineComponent line)
        {
            Lines.Remove(line);
            line.Entity.Destroy();
            _loopController.RequestLoop();
        }

        /// <summary>
        /// Withdraws everything this block has written.
        /// </summary>
        /// <remarks>
        /// Undo used to call this, because undoing a command meant erasing what it had
        /// said. It does not any more: `undo` is a command of its own and writes its
        /// own line, so the block it undoes stays on screen as a record of what
        /// happened. Kept because clearing a block is a reasonable thing for a host to
        /// want, and it is two lines.
        /// </remarks>
        public void Clear()
        {
            foreach (var line in Lines)
            {
                line.Entity.Destroy();
            }

            Lines.Clear();
            _loopController.RequestLoop();
        }

        private sealed class BlockLine : IOutputLine
        {
            private readonly LoopController _loopController;

            public BlockLine(LineComponent line, LoopController loopController)
            {
                Line = line;
                _loopController = loopController;
            }

            public LineComponent Line { get; }

            // The core's output interface has no description field: a written run is
            // just text. "output" is the style name the scene uses for it.
            public IOutputText Write(string text)
            {
                var written = new BlockText(Line.LinkNewTextBlock("output", text), _loopController);
                _loopController.RequestLoop();
                return written;
            }
        }

        private sealed class BlockText : IOutputText
        {
            private readonly TextComponent _component;
            private readonly LoopController _loopController;

            public BlockText(TextComponent component, LoopController loopController)
            {
                _component = component;
                _loopController = loopController;
            }

            public string Text
            {
                get => _component.Text;
                set
                {
                    _component.Text = value;
                    _loopController.RequestLoop();
                }
            }

            string IOutputText.Text
            {
                get => Text;
                set => Text = value;
            }
        }
    }
}
