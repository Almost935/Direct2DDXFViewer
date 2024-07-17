using Direct2DDXFViewer.BitmapHelpers;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer
{
    public class QuadTree
    {
        public ZoomBitmap _zoomBitmap = new();

        public QuadTreeNode Root { get; private set; }
        public float Zoom { get; private set; }
        public double MaxSize { get; private set; }
        public int Levels { get; private set; }

        public QuadTree(BitmapRenderTarget bitmapRenderTarget, float zoom, double maxSize)
        {
            RenderBitmap(bitmapRenderTarget, zoom);
            var bitmap = bitmapRenderTarget.Bitmap;
            Zoom = zoom;
            MaxSize = maxSize;
            GetLevels(maxSize, bitmapRenderTarget.Size);

            var bounds = new RawRectangleF(0, 0, bitmap.Size.Width, bitmap.Size.Height);
            Root = new QuadTreeNode(bounds, bitmap);
            Root.Subdivide(bitmapRenderTarget, Levels);
        }

        private void RenderBitmap(RenderTarget bitmapRenderTarget, float zoom)
        {
            _zoomBitmap.Zoom = zoom;
            _zoomBitmap.Size = size;
            _zoomBitmap.Dpi = new(96 * zoom, 96 * zoom);
            _zoomBitmap.Rect = new(0, 0, RenderTargetSize.Width * zoom, RenderTargetSize.Height * zoom);
            _zoomBitmap.BitmapRenderTarget = new BitmapRenderTarget(_renderTarget, CompatibleRenderTargetOptions.None, size)
            {
                DotsPerInch = _zoomBitmap.Dpi,
                AntialiasMode = AntialiasMode.PerPrimitive,
            };
            DrawDxfBitmapObjects(_zoomBitmap.BitmapRenderTarget, zoom);
        }
        private void GetLevels(double maxSize, Size2F renderTargetSize)
        {
            if (renderTargetSize.Width > renderTargetSize.Height)
            {
                Levels = (int)Math.Ceiling(renderTargetSize.Width / maxSize);
            }
            else
            {
                Levels = (int)Math.Ceiling(renderTargetSize.Height / maxSize);
            }
        }
    }
}
