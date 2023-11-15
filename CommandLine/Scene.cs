using CommandLine.Modules;
using Commands;
using Commands.Parser;
using Commands.Parser.SemanticTree;
using UIComponents;
using UIComponents.Components;
using EntityComponentSystem;
using Microsoft.Extensions.DependencyInjection;
using OneOf;
using Rendering.Components;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Terminal.Naming;
using Terminal.Search;
using UIComponents.Compoents.Console;
using Rendering;
using Controller;

namespace Terminal
{
    public class Scene
    {
        private readonly ECS _ecs;
        private readonly LoopController _loopController;

        public event EventHandler<EventArgs> OnInit;

        public Scene(ECS ecs,
                     LoopController loopController)
        {
            _ecs = ecs;
            _loopController = loopController;
        }

        public UICamera Camera { get; private set; }
        public ConsoleOutputPanel Output { get; private set; }
        public ConsoleInputPanel Input { get; private set; }
        public ConsoleLayout Layout { get; private set; }
        public CursorComponent Cursor { get; private set; }

        public void SetupScene()
        {
            Camera = _ecs.NewEntity("MainCamera").AddComponent<UICamera>();

            Output = _ecs.NewEntity("Output").AddComponent<ConsoleOutputPanel>();
            Input = _ecs.NewEntity("Input").AddComponent<ConsoleInputPanel>();

            Layout = _ecs.NewEntity("Layout").AddComponent<ConsoleLayout>();
            Output.Entity.Parent = Layout.Entity;
            Input.Entity.Parent = Layout.Entity;

            Entity cursorEntity = _ecs.NewEntity("Cursor");
            Cursor = cursorEntity.AddComponent<CursorComponent>();
            //cursorEntity.Parent = Layout.Entity;

            _loopController.RequestLoop();
        }

    }
}
