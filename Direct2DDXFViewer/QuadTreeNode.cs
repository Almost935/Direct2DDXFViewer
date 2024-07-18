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
        public Rect Bounds { get; private set; }
        public Bitmap Bitmap { get; private set; }
        public List<QuadTreeNode> ChildNodes { get; private set; }
        public float Zoom { get; private set; }
        public Size2F Dpi { get; private set; }

        public QuadTreeNode(Rect bounds, Bitmap bitmap, float zoom, Size2F dpi)
        {
            Bounds = bounds;
            Bitmap = bitmap;
            Zoom = zoom;
            ChildNodes = new();
            Dpi = dpi;
        }

        public void Subdivide(RenderTarget renderTarget, int level)
        {
            double halfWidth = Math.Abs((Bounds.Right - Bounds.Left) / 2);
            double halfHeight = Math.Abs((Bounds.Bottom - Bounds.Top) / 2);

            Debug.WriteLine($"\nlevel: {level} Zoom: {Zoom} halfWidth: {halfWidth} halfHeight: {halfHeight}");

            var childBounds = new[]
            {
            new Rect(Bounds.Left * Zoom, Bounds.Top * Zoom, (Bounds.Left + halfWidth) * Zoom, (Bounds.Top + halfHeight) * Zoom),
            new Rect((Bounds.Left + halfWidth) * Zoom, Bounds.Top * Zoom, Bounds.Right * Zoom, (Bounds.Top + halfHeight) * Zoom),
            new Rect(Bounds.Left * Zoom, (Bounds.Top + halfHeight) * Zoom, (Bounds.Left + halfWidth) * Zoom, Bounds.Bottom * Zoom),
            new Rect((Bounds.Left+ halfWidth) * Zoom, (Bounds.Top + halfHeight) * Zoom, Bounds.Right * Zoom, Bounds.Bottom * Zoom),
            };

            foreach (var bound in childBounds)
            {
                using (var childRenderTarget = new BitmapRenderTarget(renderTarget, CompatibleRenderTargetOptions.None, new Size2F((float)halfWidth, (float)halfHeight)))
                {
                    childRenderTarget.DotsPerInch = Dpi;
                    childRenderTarget.AntialiasMode = AntialiasMode.PerPrimitive;
                    childRenderTarget.BeginDraw();
                    childRenderTarget.DrawBitmap(Bitmap,
                        new RawRectangleF((float)bound.Left, (float)bound.Top, (float)bound.Right, (float)bound.Bottom),
                        1.0f, BitmapInterpolationMode.Linear,
                        new RawRectangleF((float)bound.Left, (float)bound.Top, (float)bound.Right, (float)bound.Bottom));
                    childRenderTarget.EndDraw();

                    ChildNodes.Add(new QuadTreeNode(bound, childRenderTarget.Bitmap, Zoom, Dpi));
                }
            }

            foreach (var child in ChildNodes)
            {
                child.Subdivide(renderTarget, level - 1);
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
