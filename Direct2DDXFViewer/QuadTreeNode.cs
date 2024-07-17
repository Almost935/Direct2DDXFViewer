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

        public QuadTreeNode(Rect bounds, Bitmap bitmap)
        {
            Bounds = bounds;
            Bitmap = bitmap;
            ChildNodes = new();
        }

        public void Subdivide(RenderTarget renderTarget, int level)
        {
            Debug.WriteLine($"level: {level}");
            if (level <= 0) { return; }

            double halfWidth = Math.Abs((Bounds.Right - Bounds.Left) / 2);
            double halfHeight = Math.Abs((Bounds.Bottom - Bounds.Top) / 2);

            var childBounds = new[]
            {
            new Rect(Bounds.Left, Bounds.Top, Bounds.Left + halfWidth, Bounds.Top + halfHeight),
            new Rect(Bounds.Left + halfWidth, Bounds.Top, Bounds.Right, Bounds.Top + halfHeight),
            new Rect(Bounds.Left, Bounds.Top + halfHeight, Bounds.Left + halfWidth, Bounds.Bottom),
            new Rect(Bounds.Left + halfWidth, Bounds.Top + halfHeight, Bounds.Right, Bounds.Bottom),
            };

            foreach (var bound in childBounds)
            {
                using (var childRenderTarget = new BitmapRenderTarget(renderTarget, CompatibleRenderTargetOptions.None, new Size2F((float)halfWidth, (float)halfHeight)))
                {
                    childRenderTarget.BeginDraw();
                    childRenderTarget.Clear(new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));

                    childRenderTarget.DrawBitmap(Bitmap,
                        new RawRectangleF((float)bound.Left, (float)bound.Top, (float)bound.Right, (float)bound.Bottom),
                        1.0f, BitmapInterpolationMode.Linear,
                        new RawRectangleF((float)bound.Left, (float)bound.Top, (float)bound.Right, (float)bound.Bottom));
                    childRenderTarget.EndDraw();

                    ChildNodes.Add(new QuadTreeNode(bound, childRenderTarget.Bitmap));
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
