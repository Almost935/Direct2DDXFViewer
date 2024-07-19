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
        private float _maxBitmapSize;

        public Bitmap OverallBitmap { get; set; }
        public QuadTreeNode Root { get; private set; }
        public float Zoom { get; private set; }
        public int Levels { get; private set; }
        public Size2F Size { get; set; }
        public Size2F Dpi { get; set; }

        public QuadTree(RenderTarget renderTarget, Bitmap overallBitmap, float zoom, ResourceCache resCache, float maxBitmapSize, Size2F dpi)
        {
            OverallBitmap = overallBitmap;
            _renderTarget = renderTarget;
            Zoom = zoom;
            _resCache = resCache;
            _maxBitmapSize = maxBitmapSize;
            Dpi = dpi;
            GetLevels(_maxBitmapSize, OverallBitmap.Size);

            var bounds = new Rect(0, 0, OverallBitmap.Size.Width, OverallBitmap.Size.Height);
            Root = new QuadTreeNode(bounds, OverallBitmap, Zoom, Dpi, _maxBitmapSize);
            Root.Subdivide(_renderTarget, Levels);
        }

        private void GetLevels(double maxSize, Size2F renderTargetSize)
        {
            if (renderTargetSize.Width > renderTargetSize.Height)
            {
                if (renderTargetSize.Width < maxSize) { Levels = 1; return; }

                Levels = 0;
                float size = renderTargetSize.Width;
                while (size > maxSize)
                {
                    Levels++;
                    size /= 2;
                }
                return;
            }
            else
            {
                if (renderTargetSize.Height < maxSize) { Levels = 1; return; }

                Levels = 0;
                float size = renderTargetSize.Height;
                while (size > maxSize)
                {
                    Levels++;
                    size /= 2;
                }
                return;
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
