using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace CommandLineReimagine.Rendering
{
    public enum RenderState
    {
        Idle,
        Drawing,
        Rendering
    } 
    internal class BitmapBuffer : IDisposable
    {
        private readonly object _lock = new object();
        private readonly Bitmap _bmp;
        private readonly Graphics _gfx;
        private RenderState state = RenderState.Idle;

        public int Width { get; }
        public int Height { get; }

        public Graphics Gfx
        {
            get
            {
                if (state != RenderState.Drawing)
                {
                    throw new InvalidOperationException("Cannot start while writing");
                }

                return _gfx;
            }
        }

        public bool IsIdle
        {
            get
            {
                lock (_lock)
                {
                    return state == RenderState.Idle;
                }
            }
        }

        public bool IsDrawing
        {
            get
            {
                lock (_lock)
                {
                    return state == RenderState.Drawing;
                }
            }
        }

        public bool IsRendering
        {
            get
            {
                lock (_lock)
                {
                    return state == RenderState.Rendering;
                }
            }
        }

        public BitmapBuffer(int width, int height)
        {
            Width = width;
            Height = height;

            _bmp = new Bitmap(Width, Height);
            _gfx = Graphics.FromImage(_bmp);

            _gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            _gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        }

        public void MarkAsIdle()
        {
            lock (_lock)
            {
                if (state != RenderState.Rendering)
                {
                    throw new InvalidOperationException("Cannot start while writing");
                }

                state = RenderState.Idle;
            }
        }

        public void MarkAsDrawing()
        {
            lock (_lock)
            {
                if (state != RenderState.Idle)
                {
                    throw new InvalidOperationException("Cannot start while writing");
                }

                state = RenderState.Drawing;

                _gfx.Clear(Color.Navy);
            }
        }

        public void MarkAsRendering()
        {
            lock (_lock)
            {
                if (state != RenderState.Drawing)
                {
                    throw new InvalidOperationException("Cannot get bitmap while writing");
                }

                state = RenderState.Rendering;
            }
        }

        public Bitmap ExtractFinishedFrame()
        {
            lock (_lock)
            {
                if (state != RenderState.Rendering)
                {
                    throw new InvalidOperationException("Cannot start while writing");
                }

                return _bmp;
            }
        }

        public void Dispose()
        {
            _bmp.Dispose();
            _gfx.Dispose();
        }

    }
}
