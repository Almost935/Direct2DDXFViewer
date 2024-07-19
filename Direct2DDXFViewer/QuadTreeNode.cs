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

                Debug.WriteLine($"\nlevel: {level} Zoom: {Zoom} halfWidth: {halfWidth} halfHeight: {halfHeight}");

                var childBounds = new[]
                {
            new Rect(Bounds.Left, Bounds.Top, (Bounds.Left + halfWidth), (Bounds.Top + halfHeight)),
            new Rect((Bounds.Left + halfWidth), Bounds.Top, Bounds.Right, (Bounds.Top + halfHeight)),
            new Rect(Bounds.Left, (Bounds.Top + halfHeight), (Bounds.Left + halfWidth), Bounds.Bottom),
            new Rect((Bounds.Left+ halfWidth), (Bounds.Top + halfHeight), Bounds.Right, Bounds.Bottom),
            };

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
