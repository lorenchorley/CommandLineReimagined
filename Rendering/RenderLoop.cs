using Rendering.Configuration;
using Microsoft.Extensions.Options;
using System.Drawing;
using System.Timers;
using EntityComponentSystem;
using EntityComponentSystem.Serialisation;

namespace Rendering;

public class RenderLoop
{
    private readonly object _lock = new object();
    private readonly ECS _ecs;
    private readonly ComponentRenderPipeline _componentRenderPipeline;
    private BitmapBuffer _buffer;
    //private Action<Graphics, float, float> _draw;
    //private System.Timers.Timer _timer;
    private Action<Bitmap, Action> _renderToScreen;
    //private bool _isActive = false;
    //private bool _isCurrentlyRefreshing = false;
    //private Task? EnqueuedRefreshTask = null;

    public RenderLoop(ECS ecs, IOptions<RenderingOptions> options, ComponentRenderPipeline componentRenderPipeline)
    {
        _ecs = ecs;
        _componentRenderPipeline = componentRenderPipeline;
        //_timer = new();
        //_timer.Interval = 1000 / options.Value.FrameRate;
        //_timer.Elapsed += TimerElapsed;
    }

    //public void SetActive(bool isActive)
    //{
    //    _isActive = isActive;

    //    if (_isActive)
    //    {
    //        _timer.Start();
    //    }
    //    else
    //    {
    //        _timer.Stop();
    //    }
    //}

    //public void RefreshOnce()
    //{
    //    lock (_lock)
    //    {
    //        //if (!_isActive)
    //        //{
    //        //    return;
    //        //}

    //        if (EnqueuedRefreshTask is not null)
    //        {
    //            return;
    //        }

    //        // Enqueue si pas déjà demandé
    //        EnqueuedRefreshTask = Task.Delay(15).ContinueWith(_ => Refresh());
    //    }
    //}

    //private void TimerElapsed(object? sender, ElapsedEventArgs e) => Refresh();
    public void Update()
    {
        lock (_lock) // TODO remove
        {
            if (_buffer != null && _buffer.IsIdle)
            {
                _buffer.MarkAsDrawing();
                _componentRenderPipeline.Draw(_buffer.Gfx, _buffer.Width, _buffer.Height);
                _buffer.MarkAsRendering();
                _renderToScreen(_buffer.ExtractFinishedFrame(), _buffer.MarkAsIdle);
            }

            //EnqueuedRefreshTask = null;
        }
    }

    public void SetCanvasSize(int width, int height)
    {
        _buffer = new BitmapBuffer(width, height);
    }

    //public void SetDrawAction(Action<Graphics, float, float> draw)
    //{
    //    _draw = draw;
    //}

    public void SetRenderToScreenAction(Action<Bitmap, Action> renderToScreen)
    {
        _renderToScreen = renderToScreen;
    }
}
