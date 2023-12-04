using EntityComponentSystem;
using Rendering;
using Rendering.Components;

namespace Controller;
public class LoopController
{
    private readonly object _requestLock = new object();
    private Task? EnqueuedRefreshTask = null;

    private readonly ECS _ecs;
    private readonly RenderLoop _renderLoop;

    public LoopController(ECS ecs, RenderLoop renderLoop)
    {
        _ecs = ecs;
        _renderLoop = renderLoop;
    }

    public void Start()
    {
        RequestLoop();
    }

    // Need to shift the responsibility of the main loop to here
    // instead of letting input action dictate the thread and the timing of actions.
    private void MainLoop()
    {
        lock (_requestLock) // Not sure if necessary, also means that other threads can be locked while render loop is running
        {
            // Not ideal that this function is here, but it needs to be done sometime after all operations were completed
            // Might be able to set up a more generalised loop that takes into account the render cycle, the object life cycle, etc
            _ecs.Update();

            UpdateLayoutsInActiveTree(); // Should produce a number of UITransformDifferentials

            ECS.ShadowECS shadowECS = _ecs.TriggerMerge();

            _renderLoop.Update(shadowECS);

            EnqueuedRefreshTask = null;
        }
    }

    private void UpdateLayoutsInActiveTree()
    {
        List<UILayoutComponent> layouts = ComponentTreeSearchLowestFirst<UILayoutComponent>(_ecs.Entities).ToList();

        // Première passe pour savoir quels élements devraient y être et où il faut les placer
        foreach (var layout in layouts)
        {
            layout.RecalculateChildTransforms();
        }
    }

    private IEnumerable<T> ComponentTreeSearchLowestFirst<T>(IEnumerable<Entity> entites) where T : Component
    {
        // Search for all uilayoutcomponents and recursively return them from the lowest layer first
        foreach (var entity in entites)
        {
            var children = entity.Children.ToArray();
            foreach (var layout in ComponentTreeSearchLowestFirst<T>(children))
            {
                yield return layout;
            }

            if (entity.TryGetComponent(out T? match))
            {
                yield return match;
            }
        }
    }

    public void RequestLoop()
    {
        lock (_requestLock)
        {
            //if (!_isActive)
            //{
            //    return;
            //}

            if (EnqueuedRefreshTask is not null)
            {
                return;
            }

            // Enqueue si pas déjà demandé
            EnqueuedRefreshTask = Task.Delay(15).ContinueWith(_ => MainLoop());
        }
    }

}
