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
using System.Windows.Media;

namespace Direct2DDXFViewer
{
    public class QuadTree : IDisposable
    {
        #region Fields
        private RenderTarget _renderTarget;
        private float _maxBitmapSize;
        private bool _disposed = false;
        #endregion

        #region Properties
        public Bitmap OverallBitmap { get; set; }
        public Rect TreeBounds { get; set; }
        public Rect DestRect { get; set; }
        public QuadTreeNode Root { get; private set; }
        public float Zoom { get; private set; }
        public int Levels { get; private set; }
        public Size2F Size { get; set; }
        public Size2F Dpi { get; set; }
        #endregion

        #region Constructors
        public QuadTree(RenderTarget renderTarget, Bitmap overallBitmap, float zoom, float maxBitmapSize, Size2F dpi, Rect treeBounds, Rect destRect)
        {
            OverallBitmap = overallBitmap;
            _renderTarget = renderTarget;
            Zoom = zoom;
            _maxBitmapSize = maxBitmapSize;
            Dpi = dpi;
            GetLevels(_maxBitmapSize, OverallBitmap.Size);

            //TreeBounds = new Rect(0, 0, OverallBitmap.Size.Width, OverallBitmap.Size.Height);
            //DestRect = new(0, 0, renderTarget.Size.Width, renderTarget.Size.Height);
            TreeBounds = treeBounds;
            DestRect = destRect;

            Root = new QuadTreeNode(TreeBounds, DestRect, OverallBitmap, Zoom, Dpi, _maxBitmapSize);
            Root.Subdivide(_renderTarget, Levels);
        }
        #endregion

        #region Methods

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

        public List<QuadTreeNode> GetQuadTreeView(Rect view)
        {
            List<QuadTreeNode> quadTreeNodes = new();
            quadTreeNodes.AddRange(Root.GetIntersectingQuadTreeNodes(view));

            return quadTreeNodes;
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
                    OverallBitmap?.Dispose();
                    Root?.Dispose();
                }

                // Free unmanaged resources (unmanaged objects) and override a finalizer below.
                // Set large fields to null.
                _renderTarget = null;
                OverallBitmap = null;
                Root = null;

                _disposed = true;
            }
        }
        ~QuadTree()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }
        #endregion
    }
}
