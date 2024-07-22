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
using Direct2DDXFViewer.BitmapHelpers;
using System.Diagnostics;

namespace Direct2DDXFViewer
{
    public class QuadTreeCache
    {
        #region Fields
        private RenderTarget _renderTarget;
        private Size2F _renderTargetSize;
        private Factory1 _factory;
        private ObjectLayerManager _layerManager;
        private Rect _extents;
        private Matrix _extentsMatrix;
        private ResourceCache _resCache;
        private QuadTree _maxSizeQuadTree;
        private float _zoomFactor;
        private int _initialLoadFactor = 5;

        private const float _maxBitmapSize = 1000;
        #endregion

        #region Properties
        public Dictionary<double, QuadTree> QuadTrees { get; private set; }
        public QuadTree CurrentQuadTree { get; private set; }
        public QuadTree InitialQuadTree { get; private set; }
        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="QuadTreeCache"/> class.
        /// </summary>
        /// <param name="renderTarget">The render target.</param>
        /// <param name="factory">The factory.</param>
        /// <param name="layerManager">The layer manager.</param>
        /// <param name="extents">The extents.</param>
        /// <param name="extentsMatrix">The extents matrix.</param>
        /// <param name="resCache">The resource cache.</param>
        public QuadTreeCache(RenderTarget renderTarget, Factory1 factory, ObjectLayerManager layerManager, Rect extents, Matrix extentsMatrix, ResourceCache resCache, float zoomFactor)
        {
            _renderTarget = renderTarget;
            _renderTargetSize = new Size2F(renderTarget.Size.Width, renderTarget.Size.Height);
            _layerManager = layerManager;
            _extents = extents;
            _factory = factory;
            _extentsMatrix = extentsMatrix;
            _resCache = resCache;
            _maxSizeQuadTree = GetMaxSizeQuadTree(renderTarget);
            _zoomFactor = zoomFactor;
            QuadTrees = new();

            Initialize();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Creates initial QuadTrees based on zoom factor.
        /// </summary>
        private void Initialize()
        {
            // Get initial (zoom = 1) QuadTree
            InitialQuadTree = GetQuadTree(1);
            CurrentQuadTree = InitialQuadTree;

            float zoom = 1;
            for (int i = 1; i <= _initialLoadFactor; i++)
            {
                zoom *= _zoomFactor;
                GetQuadTree(zoom);
            }

            zoom = 1;
            for (int i = 1; i <= _initialLoadFactor; i++)
            {
                zoom *= (1 / _zoomFactor);
                GetQuadTree(zoom);
            }
        }

        public void UpdateCurrentQuadTree(float zoom)
        {
            if (zoom == 1.0f)
            {
                CurrentQuadTree = InitialQuadTree;
            }
            else
            {
                CurrentQuadTree = GetQuadTree(zoom);
            }
        }

        /// <summary>
        /// Gets the quad tree for a given zoom level.
        /// </summary>
        /// <param name="zoom">The zoom level.</param>
        /// <returns>The quad tree for the specified zoom level.</returns>
        public QuadTree GetQuadTree(float zoom)
        {
            // If the zoom is already in the dictionary, return the quad tree
            if (QuadTrees.TryGetValue(Math.Round(zoom, 3), out QuadTree quadTree))
            {
                return quadTree;
            }

            // If render target size multiplied by the zoom is greater than the max bitmap size, return the max zoom bitmap
            Size2F size = new(_renderTargetSize.Width * zoom, _renderTargetSize.Height * zoom);
            if (size.Width > _resCache.MaxBitmapSize ||
                size.Height > _resCache.MaxBitmapSize)
            {
                return _maxSizeQuadTree;
            }

            // Create a new quad tree
            Size2F dpi = new(96.0f * zoom, 96.0f * zoom);
            BitmapRenderTarget bitmapRenderTarget = new(_renderTarget, CompatibleRenderTargetOptions.None, size)
            {
                DotsPerInch = dpi,
                AntialiasMode = AntialiasMode.PerPrimitive
            };

            bitmapRenderTarget.BeginDraw();
            bitmapRenderTarget.Clear(new RawColor4(0, 1, 0, 0.25f));

            bitmapRenderTarget.Transform = new RawMatrix3x2((float)_extentsMatrix.M11, (float)_extentsMatrix.M12, (float)_extentsMatrix.M21, (float)_extentsMatrix.M22, (float)_extentsMatrix.OffsetX, (float)_extentsMatrix.OffsetY);
            float thickness = 1.0f / (bitmapRenderTarget.Transform.M11 * zoom);
            _layerManager.Draw(bitmapRenderTarget, thickness);
            bitmapRenderTarget.EndDraw();

            quadTree = new QuadTree(_renderTarget, bitmapRenderTarget.Bitmap, zoom, _resCache, _maxBitmapSize, dpi);
            QuadTrees.Add(Math.Round((double)zoom, 3), quadTree);
            return quadTree;
        }

        /// <summary>
        /// Calculates and sets the maximum values for zoom and size based on the render target's dimensions and the maximum bitmap size.
        /// </summary>
        public QuadTree GetMaxSizeQuadTree(RenderTarget renderTarget)
        {
            //MaxZoomBitmap?.Dispose();

            Size2F maxSize;
            float maxZoom;
            Size2F maxDpi;
            RawRectangleF maxRect;

            if (renderTarget.Size.Width > renderTarget.Size.Height)
            {
                maxSize = new(_resCache.MaxBitmapSize, _resCache.MaxBitmapSize * (renderTarget.Size.Height / renderTarget.Size.Width));
                maxZoom = maxSize.Height / renderTarget.Size.Height;
                maxDpi = new(96.0f * maxZoom, 96.0f * maxZoom);
                maxRect = new(0, 0, renderTarget.Size.Width * maxZoom, renderTarget.Size.Height * maxZoom);
            }
            else
            {
                maxSize = new(_resCache.MaxBitmapSize, _resCache.MaxBitmapSize * (renderTarget.Size.Width / renderTarget.Size.Height));
                maxZoom = maxSize.Width / renderTarget.Size.Width;
                maxDpi = new(96.0f * maxZoom, 96.0f * maxZoom);
                maxRect = new(0, 0, renderTarget.Size.Width * maxZoom, renderTarget.Size.Height * maxZoom);
            }

            BitmapRenderTarget bitmapRenderTarget = new(renderTarget, CompatibleRenderTargetOptions.None, maxSize)
            {
                DotsPerInch = maxDpi,
                AntialiasMode = AntialiasMode.PerPrimitive
            };
            bitmapRenderTarget.Transform = new RawMatrix3x2((float)_extentsMatrix.M11, (float)_extentsMatrix.M12, (float)_extentsMatrix.M21, (float)_extentsMatrix.M22, (float)_extentsMatrix.OffsetX, (float)_extentsMatrix.OffsetY);

            float thickness = 1.0f / (bitmapRenderTarget.Transform.M11 * maxZoom);

            bitmapRenderTarget.BeginDraw();
            bitmapRenderTarget.Clear(new RawColor4(0, 1, 0, 0.25f));
            _layerManager.Draw(bitmapRenderTarget, thickness);
            bitmapRenderTarget.EndDraw();



            QuadTree maxSizeQuadTree = new(renderTarget, bitmapRenderTarget.Bitmap, maxZoom, _resCache, _maxBitmapSize, maxDpi);
            return maxSizeQuadTree;
        }
        #endregion
    }
}
