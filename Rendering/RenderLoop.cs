using EntityComponentSystem;
using InteractionLogic;
using Microsoft.Extensions.Options;
using Rendering.Configuration;
using System.Drawing;

namespace Rendering;

public class RenderLoop
{
    private Font _font = new Font(FontFamily.GenericMonospace, 14);

    private readonly object _lock = new object();

    private readonly ComponentRenderPipeline _componentRenderPipeline;

    private BitmapBuffer _buffer;
    private Action<Bitmap, Action> _renderToScreen;

    //private Action<Graphics, float, float> _draw;
    //private System.Timers.Timer _timer;
    //private bool _isActive = false;
    //private bool _isCurrentlyRefreshing = false;
    //private Task? EnqueuedRefreshTask = null;

    public RenderLoop(IOptions<RenderingOptions> options, ComponentRenderPipeline componentRenderPipeline, ICanvasEventEmitter canvasEventEmitter)
    {
        _componentRenderPipeline = componentRenderPipeline;

        //_timer = new();
        //_timer.Interval = 1000 / options.Value.FrameRate;
        //_timer.Elapsed += TimerElapsed;

    }

    public void Update(ECS.ShadowECS shadowECS)
    {
        lock (_lock) // TODO remove
        {
            if (_buffer != null && _buffer.IsIdle)
            {
                _buffer.MarkAsDrawing();
                _componentRenderPipeline.Draw(_buffer.Gfx, _buffer.Width, _buffer.Height, shadowECS);
                _buffer.MarkAsRendering();
                _renderToScreen(_buffer.ExtractFinishedFrame(), _buffer.MarkAsIdle);
            }

            //EnqueuedRefreshTask = null;
        }
    }

    float bidouilleHorizontalRatio = 0.655f;
    float bidouilleVerticalRatio = 0.85f;

    public SizeF GetLetterSize()
    {
        SizeF letterSize = _buffer.GetLetterSize(_font);

        return new(letterSize.Width * bidouilleHorizontalRatio, letterSize.Height * bidouilleVerticalRatio);
    }

    public void SetCanvasSize(int width, int height)
    {
        _buffer = new BitmapBuffer(width, height);
    }

    public void SetRenderToScreenAction(Action<Bitmap, Action> renderToScreen)
    {
        _renderToScreen = renderToScreen;
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

    //public void SetDrawAction(Action<Graphics, float, float> draw)
    //{
    //    _draw = draw;
    //}

}
