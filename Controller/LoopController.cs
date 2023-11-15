using EntityComponentSystem;
using Rendering;
using System.Security.Cryptography;

namespace Controller;
public class LoopController
{
    private readonly object _lock = new object();
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

    }

    private void MainLoop()
    {
        // Not ideal that this function is here, but it needs to be done sometime after all operations were completed
        // Might be able to set up a more generalised loop that takes into account the render cycle, the object life cycle, etc
        _ecs.Update();

        _renderLoop.Update();

        EnqueuedRefreshTask = null;
    }


    public void RequestLoop()
    {
        lock (_lock)
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
