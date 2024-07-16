using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer
{
    public class QuadTreeNode
    {
        public RawRectangleF Bounds { get; private set; }
        public Bitmap Bitmap { get; private set; }
        public List<QuadTreeNode> Children { get; private set; }

        public QuadTreeNode(RawRectangleF bounds, Bitmap bitmap)
        {
            Bounds = bounds;
            Bitmap = bitmap;
            Children = new List<QuadTreeNode>();
        }

        public void Subdivide(RenderTarget renderTarget, int level)
        {
            if (level <= 0) { return; }

            float halfWidth = (Bounds.Right - Bounds.Left) / 2;
            float halfHeight = (Bounds.Bottom - Bounds.Top) / 2;

            var childBounds = new[]
            {
            new RawRectangleF(Bounds.Left, Bounds.Top, Bounds.Left + halfWidth, Bounds.Top + halfHeight),
            new RawRectangleF(Bounds.Left + halfWidth, Bounds.Top, Bounds.Right, Bounds.Top + halfHeight),
            new RawRectangleF(Bounds.Left, Bounds.Top + halfHeight, Bounds.Left + halfWidth, Bounds.Bottom),
            new RawRectangleF(Bounds.Left + halfWidth, Bounds.Top + halfHeight, Bounds.Right, Bounds.Bottom),
        };

            foreach (var bound in childBounds)
            {
                var childBitmap = new Bitmap(renderTarget, new Size2((int)halfWidth, (int)halfHeight), new BitmapProperties(renderTarget.PixelFormat));
                using (var childRenderTarget = new BitmapRenderTarget(renderTarget, CompatibleRenderTargetOptions.None, new Size2F(halfWidth, halfHeight)))
                {
                    childRenderTarget.BeginDraw();
                    childRenderTarget.DrawBitmap(Bitmap, bound, 1.0f, BitmapInterpolationMode.Linear, bound);
                    childRenderTarget.EndDraw();
                    childRenderTarget.Flush();

                    Children.Add(new QuadTreeNode(bound, childBitmap));
                }
            }

            foreach (var child in Children)
            {
                child.Subdivide(renderTarget, level - 1);
            }
        }
    }
}
