using Direct2DControl;
using Direct2DDXFViewer.DrawingObjects;
using SharpDX.Direct2D1;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using SharpDX.Mathematics.Interop;

namespace Direct2DDXFViewer
{
    public class QuadTreeCache
    {
        #region Fields
        private RenderTarget _renderTarget;
        private Factory1 _factory;
        private ObjectLayerManager _layerManager;
        private Rect _extents;
        private Matrix _extentsMatrix;
        private ResourceCache _resCache;
        private QuadTree _maxSizeQuadTree;
        private Size2F _maxSize;

        private const float maxSize = 5000;
        #endregion

        #region Properties
        public Dictionary<float, QuadTree> QuadTrees { get; private set; }
        #endregion

        #region Constructors
        public QuadTreeCache(RenderTarget renderTarget, Factory1 factory, ObjectLayerManager layerManager, Rect extents, Matrix extentsMatrix, ResourceCache resCache)
        {
            _renderTarget = renderTarget;
            _layerManager = layerManager;
            _extents = extents;
            _factory = factory;
            _extentsMatrix = extentsMatrix;
            _resCache = resCache;

            QuadTrees = new();
        }
        #endregion

        #region Methods
        public void GetQuadTree(float zoom)
        {
            if (QuadTrees.ContainsKey(zoom)) { return; }

            using (var bitmapRenderTarget = new BitmapRenderTarget(_renderTarget, CompatibleRenderTargetOptions.None, _renderTargetSize))
            {
                bitmapRenderTarget.Transform = new RawMatrix3x2((float)_extentsMatrix.M11, (float)_extentsMatrix.M12, (float)_extentsMatrix.M21, (float)_extentsMatrix.M22, (float)_extentsMatrix.OffsetX, (float)_extentsMatrix.OffsetY);

                float thickness = 1.0f / (bitmapRenderTarget.Transform.M11 * zoom);

                bitmapRenderTarget.BeginDraw();
                _layerManager.Draw(bitmapRenderTarget, thickness);
                bitmapRenderTarget.EndDraw();
                bitmapRenderTarget.Flush();

                QuadTrees.Add(zoom, new QuadTree(bitmapRenderTarget, zoom, maxSize));
            }
        }
        public void GetMaxValues()
        {
            //MaxZoomBitmap?.Dispose();

            if (_renderTarget.Size.Width > _renderTarget.Size.Height)
            {
                _maxSize = new(_resCache.MaxBitmapSize, _resCache.MaxBitmapSize * (_renderTarget.Size.Height / _renderTarget.Size.Width));
            }
            else
            {
                _maxSize = new(_resCache.MaxBitmapSize, _resCache.MaxBitmapSize * (_renderTarget.Size.Height / _renderTarget.Size.Width));
            }

            float maxZoom = _maxSize.Height / _renderTarget.Size.Height;
            Size2F maxDpi = new(96.0f * maxZoom, 96.0f * maxZoom);
            RawRectangleF maxRect = new(0, 0, _renderTarget.Size.Width * maxZoom, _renderTarget.Size.Height * maxZoom);

            using (var bitmapRenderTarget = new BitmapRenderTarget(_renderTarget, CompatibleRenderTargetOptions.None, _renderTarget.Size))
            {
                bitmapRenderTarget.Transform = new RawMatrix3x2((float)_extentsMatrix.M11, (float)_extentsMatrix.M12, (float)_extentsMatrix.M21, (float)_extentsMatrix.M22, (float)_extentsMatrix.OffsetX, (float)_extentsMatrix.OffsetY);

                float thickness = 1.0f / (bitmapRenderTarget.Transform.M11 * maxZoom);

                bitmapRenderTarget.BeginDraw();
                _layerManager.Draw(bitmapRenderTarget, thickness);
                bitmapRenderTarget.EndDraw();
                bitmapRenderTarget.Flush();

                _maxSizeQuadTree = new QuadTree(bitmapRenderTarget, maxZoom, maxSize);
            }
        }
        #endregion
    }
}
