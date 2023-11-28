using Controller;
using EntityComponentSystem;
using Rendering.Components;
using UIComponents;
using UIComponents.Compoents.Console;
using UIComponents.Components;

namespace Terminal
{
    public class Scene : IECSSystem
    {
        private readonly ECS _ecs;
        private readonly LoopController _loopController;

        public Scene(ECS ecs,
                     LoopController loopController)
        {
            _ecs = ecs;
            _loopController = loopController;
        }

        public UICamera Camera { get; private set; }
        public ConsoleOutputPanel OutputPanel { get; private set; }
        public ConsoleInputPanel InputPanel { get; private set; }
        public ConsoleLayout Layout { get; private set; }
        public CursorComponent Cursor { get; private set; }
        public MouseInputHandler MouseInputHandler { get; private set; }
        public KeyInputHandler KeyInputHandler { get; private set; }

        public void OnInit()
        {
            Camera = _ecs.NewEntity("MainCamera").AddComponent<UICamera>();

            OutputPanel = _ecs.NewEntity("Output").AddComponent<ConsoleOutputPanel>();
            InputPanel = _ecs.NewEntity("Input").AddComponent<ConsoleInputPanel>();

            Layout = _ecs.NewEntity("Layout").AddComponent<ConsoleLayout>();
            OutputPanel.Entity.Parent = Layout.Entity;
            InputPanel.Entity.Parent = Layout.Entity;

            MouseInputHandler = _ecs.NewEntity("MouseInputHandler").AddComponent<MouseInputHandler>();
            KeyInputHandler = _ecs.NewEntity("KeyInputHandler").AddComponent<KeyInputHandler>();

            Entity cursorEntity = _ecs.NewEntity("Cursor");
            Cursor = cursorEntity.AddComponent<CursorComponent>();
            //cursorEntity.Parent = Layout.Entity;

            _loopController.RequestLoop();
        }

        public void OnStart()
        {

        }

    }
}
