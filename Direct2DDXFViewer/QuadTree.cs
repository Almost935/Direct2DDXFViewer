using Direct2DDXFViewer.BitmapHelpers;
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
        public QuadTreeNode Root { get; private set; }
        public float Zoom { get; private set; }
        public double MaxSize { get; private set; }

        public QuadTree(BitmapRenderTarget renderTarget, float zoom, double maxSize)
        {
            var bitmap = renderTarget.Bitmap;
            Zoom = zoom;
            MaxSize = maxSize;
            var bounds = new RawRectangleF(0, 0, bitmap.Size.Width, bitmap.Size.Height);
            Root = new QuadTreeNode(bounds, bitmap);
            Root.Subdivide(renderTarget, levels);
        }

        public void Draw(RenderTarget renderTarget)
        {
            DrawNode(renderTarget, Root);
        }

        private void DrawNode(RenderTarget renderTarget, QuadTreeNode node)
        {
            if (node.Children.Count == 0)
            {
                renderTarget.DrawBitmap(node.Bitmap, node.Bounds, 1.0f, BitmapInterpolationMode.Linear);
            }
            else
            {
                foreach (var child in node.Children)
                {
                    DrawNode(renderTarget, child);
                }
            }
        }
        private void GetLevels(double mazSize)
        {
            double size = mazSize;
            while (size > 1)
            {
                size /= 2;
                levels++;
            }
        }
    }
}
