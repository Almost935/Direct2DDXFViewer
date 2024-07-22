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

namespace Direct2DDXFViewer
{
    public class QuadTreeNode
    {
        private float _maxBitmapSize;

        public Rect Bounds { get; private set; }
        public Bitmap Bitmap { get; private set; }
        public List<QuadTreeNode> ChildNodes { get; private set; }
        public float Zoom { get; private set; }
        public Size2F Dpi { get; private set; }

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

        public void Subdivide(RenderTarget renderTarget, int level)
        {
            if (level > 1)
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
                    //ChildNodes.Add(new QuadTreeNode(bounds, Bitmap, Zoom, Dpi, _renderTargetSize));

                    using (var childRenderTarget = new BitmapRenderTarget(renderTarget, CompatibleRenderTargetOptions.None, new Size2F((float)halfWidth, (float)halfHeight)))
                    {
                        childRenderTarget.DotsPerInch = Dpi;
                        childRenderTarget.AntialiasMode = AntialiasMode.PerPrimitive;
                        childRenderTarget.BeginDraw();
                        childRenderTarget.DrawBitmap(Bitmap,
                            new RawRectangleF((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom),
                            1.0f, BitmapInterpolationMode.Linear,
                            new RawRectangleF((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom));
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
    }
}
