using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using Direct2DControl;
using System.Xml.Linq;

namespace Direct2DDXFViewer
{
    public class QuadTreeNode : IDisposable
    {
        #region Fields
        private float _maxBitmapSize;
        private bool _disposed = false;
        #endregion

        #region Properties
        public Rect Bounds { get; private set; }
        public Bitmap Bitmap { get; private set; }
        public List<QuadTreeNode> ChildNodes { get; private set; }
        public float Zoom { get; private set; }
        public Size2F Dpi { get; private set; }
        #endregion

        #region Constructors
        public QuadTreeNode(Rect bounds, Bitmap? bitmap, float zoom, Size2F dpi, float maxBitmapSize)
        {
            Bounds = bounds;
            Zoom = zoom;
            ChildNodes = new();
            Dpi = dpi;

            if (bitmap != null)
            {
                Bitmap = bitmap;
            }
            _maxBitmapSize = maxBitmapSize;
        }
        #endregion

        #region Methods
        public void Subdivide(RenderTarget renderTarget, int level)
        {
            if (level > 0)
            {
                double halfWidth = Math.Abs((Bounds.Right - Bounds.Left) / 2);
                double halfHeight = Math.Abs((Bounds.Bottom - Bounds.Top) / 2);

                Rect rect1 = new(new Point(Bounds.Left, Bounds.Top), new Point((Bounds.Left + halfWidth), (Bounds.Top + halfHeight)));
                Rect rect2 = new(new Point((Bounds.Left + halfWidth), Bounds.Top), new Point(Bounds.Right, (Bounds.Top + halfHeight)));
                Rect rect3 = new(new Point(Bounds.Left, (Bounds.Top + halfHeight)), new Point((Bounds.Left + halfWidth), Bounds.Bottom));
                Rect rect4 = new(new Point((Bounds.Left + halfWidth), (Bounds.Top + halfHeight)), new Point(Bounds.Right, Bounds.Bottom));

                var childBounds = new[]
                { rect1, rect2, rect3, rect4 };

                foreach (var bounds in childBounds)
                {
                    using (var childRenderTarget = new BitmapRenderTarget(renderTarget, CompatibleRenderTargetOptions.None, new Size2F((float)halfWidth, (float)halfHeight)))
                    {
                        childRenderTarget.DotsPerInch = Dpi;
                        childRenderTarget.AntialiasMode = AntialiasMode.PerPrimitive;
                        childRenderTarget.BeginDraw();

                        RawRectangleF destRect = new((float)(bounds.Left), (float)(bounds.Top), (float)(bounds.Right), (float)(bounds.Bottom));
                        //RawRectangleF destRect = new(0, 0, (float)ActualWidth, (float)ActualHeight);
                        RawRectangleF sourceRect = new((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom);
                        //RawRectangleF sourceRect = new((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom);

                        childRenderTarget.DrawBitmap(Bitmap, destRect, 1.0f, BitmapInterpolationMode.Linear, sourceRect);
                        childRenderTarget.EndDraw();

                        ChildNodes.Add(new QuadTreeNode(bounds, childRenderTarget.Bitmap, Zoom, Dpi, _maxBitmapSize));
                    }
                }

                foreach (var child in ChildNodes)
                {
                    child.Subdivide(renderTarget, level - 1);
                }
            }
        }

        public List<QuadTreeNode> GetIntersectingQuadTreeNodes(Rect view)
        {
            List<QuadTreeNode> intersectingNodes = new();

            if (this.Bounds.Contains(view) || this.Bounds.IntersectsWith(view))
            {
                if (ChildNodes.Count == 0)
                {
                    intersectingNodes.Add(this);
                }
                else
                {
                    foreach (var child in ChildNodes)
                    {
                        intersectingNodes.AddRange(child.GetIntersectingQuadTreeNodes(view));
                    }
                }
            }

            return intersectingNodes;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects).
                    Bitmap?.Dispose();
                    foreach (var child in ChildNodes)
                    {
                        child.Dispose();
                    }
                }

                // Free unmanaged resources (unmanaged objects) and override a finalizer below.
                // Set large fields to null.

                _disposed = true;
            }
        }
        ~QuadTreeNode()
        {
            Dispose(false);
        }
        #endregion
    }
}
