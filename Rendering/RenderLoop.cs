using Microsoft.Extensions.Options;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Timers;
using CommandLineReimagine.Configuration;

namespace CommandLineReimagine.Rendering
{
    public class RenderLoop
    {
        private readonly object _lock = new object();
        private BitmapBuffer _buffer;
        private Action<Graphics, float, float> _draw;
        private Timer _timer;
        private Action<Bitmap, Action> _renderToScreen;
        private bool _isActive = false;

        public RenderLoop(IOptions<RenderingOptions> options)
        {
            _timer = new();
            _timer.Interval = 1000 / options.Value.FrameRate;
            _timer.Elapsed += TimerElapsed;
        }

        internal void SetActive(bool isActive)
        {
            _isActive = isActive;

            if (_isActive)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
        }

        internal void RefreshOnce()
        {
            if (!_isActive)
            {
                Task.Run(Refresh);
            }
        }

        private void TimerElapsed(object? sender, ElapsedEventArgs e) => Refresh();
        private void Refresh()
        {
            if (_buffer == null)
            {
                return;
            }

            lock (_lock)
            {
                if (_buffer.IsIdle)
                {
                    _buffer.MarkAsDrawing();
                    _draw(_buffer.Gfx, _buffer.Width, _buffer.Height);
                    _buffer.MarkAsRendering();
                    _renderToScreen(_buffer.ExtractFinishedFrame(), _buffer.MarkAsIdle);
                }
            }
        }

        public void SetCanvasSize(int width, int height)
        {
            _buffer = new BitmapBuffer(width, height);
        }

        public void SetDrawAction(Action<Graphics, float, float> draw)
        {
            _draw = draw;
        }

        public void SetRenderToScreenAction(Action<Bitmap, Action> renderToScreen)
        {
            _renderToScreen = renderToScreen;
        }
    }
}
