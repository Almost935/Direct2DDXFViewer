using Direct2DControl;
using Direct2DDXFViewer.BitmapHelpers;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Direct2DDXFViewer
{
    public class QuadTree
    {
        private RenderTarget _renderTarget;
        private ResourceCache _resCache;

        public Bitmap OverallBitmap { get; set; }
        public QuadTreeNode Root { get; private set; }
        public float Zoom { get; private set; }
        public int Levels { get; private set; }
        public Rect Rect { get; set; }
        public Size2F Size { get; set; }
        public Size2F Dpi { get; set; }

        public QuadTree(RenderTarget renderTarget, Bitmap overallBitmap, float zoom, ResourceCache resCache)
        {
            OverallBitmap = overallBitmap;
            _renderTarget = renderTarget;
            Zoom = zoom;
            _resCache = resCache;
            GetLevels(_resCache.MaxBitmapSize, OverallBitmap.Size);

            var bounds = new Rect(0, 0, OverallBitmap.Size.Width, OverallBitmap.Size.Height);
            Root = new QuadTreeNode(bounds, OverallBitmap);
            Root.Subdivide(_renderTarget, Levels);
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

        public List<QuadTreeNode> GetQuadTreeView(Rect rect)
        {
            List<QuadTreeNode> quadTreeNodes = new();
            quadTreeNodes.AddRange(Root.GetIntersectingQuadTreeNodes(rect));

            return quadTreeNodes;
        }
    }
}
