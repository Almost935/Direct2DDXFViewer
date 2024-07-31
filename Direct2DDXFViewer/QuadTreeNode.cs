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
using System.Windows.Media;

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
        public Rect DestRect { get; private set; }
        public Bitmap Bitmap { get; private set; }
        public List<QuadTreeNode> ChildNodes { get; private set; }
        public float Zoom { get; private set; }
        public Size2F Dpi { get; private set; }
        #endregion

        #region Constructors
        public QuadTreeNode(Rect bounds, Rect destRect, Bitmap? bitmap, float zoom, Size2F dpi, float maxBitmapSize)
        {
            Bounds = bounds;
            DestRect = destRect;
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

                double destHalfWidth = Math.Abs((DestRect.Right - DestRect.Left) / 2);
                double destHalfHeight = Math.Abs((DestRect.Bottom - DestRect.Top) / 2);
                Rect destRect1 = new(new Point(DestRect.Left, DestRect.Top), new Point((DestRect.Left + destHalfWidth), (DestRect.Top + destHalfHeight)));
                Rect destRect2 = new(new Point((DestRect.Left + destHalfWidth), DestRect.Top), new Point(DestRect.Right, (DestRect.Top + destHalfHeight)));
                Rect destRect3 = new(new Point(DestRect.Left, (DestRect.Top + destHalfHeight)), new Point((DestRect.Left + destHalfWidth), DestRect.Bottom));
                Rect destRect4 = new(new Point((DestRect.Left + destHalfWidth), (DestRect.Top + destHalfHeight)), new Point(DestRect.Right, DestRect.Bottom));


                //Debug.WriteLine($"\ndestHalfWidth: {destHalfWidth} destHalfHeight: {destHalfHeight} " +
                //    $"\nDestRect: {DestRect.Left} {DestRect.Top} {DestRect.Right} {DestRect.Bottom}" +
                //    $"\ndestRect1: {destRect1.Left} {destRect1.Top} {destRect1.Right} {destRect1.Bottom}" +
                //    $"\ndestRect2: {destRect2.Left} {destRect2.Top} {destRect2.Right} {destRect2.Bottom}" +
                //    $"\ndestRect3: {destRect3.Left} {destRect3.Top} {destRect3.Right} {destRect3.Bottom}" +
                //    $"\ndestRect4: {destRect4.Left} {destRect4.Top} {destRect4.Right} {destRect4.Bottom}" +
                //    $"\nZoom: {Zoom}");

                var childBounds = new[]
                { rect1, rect2, rect3, rect4 };
                var destChildBounds = new[]
                { destRect1, destRect2, destRect3, destRect4 };

                for (int i = 0; i < childBounds.Count(); i++)
                {
                    using (var childRenderTarget = new BitmapRenderTarget(renderTarget, CompatibleRenderTargetOptions.None, new Size2F((float)halfWidth, (float)halfHeight)))
                    {
                        childRenderTarget.DotsPerInch = Dpi;
                        childRenderTarget.AntialiasMode = AntialiasMode.PerPrimitive;
                        childRenderTarget.BeginDraw();

                        RawRectangleF destRect = new((float)(childBounds[i].Left), (float)(childBounds[i].Top), (float)(childBounds[i].Right), (float)(childBounds[i].Bottom));
                        RawRectangleF sourceRect = new((float)childBounds[i].Left, (float)childBounds[i].Top, (float)childBounds[i].Right, (float)childBounds[i].Bottom);

                        childRenderTarget.DrawBitmap(Bitmap, destRect, 1.0f, BitmapInterpolationMode.Linear, sourceRect);
                        childRenderTarget.EndDraw();

                        //Debug.WriteLine($"destChildBounds[i]: {destChildBounds[i].Width} {destChildBounds[i].Height}");

                        ChildNodes.Add(new QuadTreeNode(childBounds[i], destChildBounds[i], childRenderTarget.Bitmap, Zoom, Dpi, _maxBitmapSize));
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

            if (view.Contains(DestRect) || view.IntersectsWith(DestRect))
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
                ChildNodes.Clear();
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
