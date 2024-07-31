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
using System.Collections.Concurrent;

namespace Direct2DDXFViewer
{
    public class QuadTreeCache : IDisposable
    {
        #region Fields
        private RenderTarget _renderTarget;
        private Size2F _renderTargetSize;
        private Factory1 _factory;
        private ObjectLayerManager _layerManager;
        private Matrix _extentsMatrix;
        private ResourceCache _resCache;
        private readonly float _zoomFactor;
        private int _initialLoadFactor = 2;
        private int _updatedCurrentQuadTreeLoadFactor = 2;
        private int _asyncInitialLoadFactor = 10;
        private bool _disposed = false;

        private const float _maxBitmapSize = 10000;
        #endregion

        #region Properties
        public ConcurrentDictionary<double, QuadTree> QuadTrees { get; private set; }
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
            _factory = factory;
            _extentsMatrix = extentsMatrix;
            _resCache = resCache;
            _zoomFactor = zoomFactor;
            QuadTrees = new();

            Initialize();
            GetInitialZoomQuadTreesAsync();
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
                GetAdjacentZoomQuadTreesAsync(CurrentQuadTree);
            }
            else
            {
                CurrentQuadTree = GetQuadTree(zoom);
                GetAdjacentZoomQuadTreesAsync(CurrentQuadTree);
            }
        }

        /// <summary>
        /// Gets the quad tree for a given zoom level.
        /// </summary>
        /// <param name="zoom">The zoom level.</param>
        /// <returns>The quad tree for the specified zoom level.</returns>
        public QuadTree GetQuadTree(float zoom)
        {
            Debug.WriteLine($"\nSearching for QuadTree for {zoom} factor.");
            // If the zoom is already in the dictionary, return the quad tree
            if (QuadTrees.TryGetValue(Math.Round(zoom, 3), out QuadTree quadTree))
            {
                Debug.WriteLine($"QuadTree exists");

                return quadTree;
            }

            // If render target size multiplied by the zoom is greater than the max bitmap size, return the max zoom bitmap
            Size2F size = new(_renderTargetSize.Width * zoom, _renderTargetSize.Height * zoom);
            if (size.Width > _resCache.MaxBitmapSize ||
                size.Height > _resCache.MaxBitmapSize)
            {
                Debug.WriteLine($"Max Size QuadTree Returned");
                quadTree = GetMaxSizeQuadTree(zoom);
                QuadTrees.TryAdd(Math.Round((double)zoom, 3), quadTree);

                return quadTree;
            }

            Debug.WriteLine($"New quadtree for zoom level: {zoom}");

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
            QuadTrees.TryAdd(Math.Round((double)zoom, 3), quadTree);

            return quadTree;
        }

        /// <summary>
        /// Verify's that the QuadTree's adjacent to the given QuadTree are already cached and, if not, created.
        /// </summary>
        /// <param name="currentQuadTree">The QuadTree from which the adjacent QuadTree's will be verified</param>
        /// <returns></returns>
        private async Task GetAdjacentZoomQuadTreesAsync(QuadTree currentQuadTree)
        {
            float zoom = currentQuadTree.Zoom;
            for (int i = 1; i <= _updatedCurrentQuadTreeLoadFactor; i++)
            {
                zoom *= _zoomFactor;
                await Task.Run(() => GetQuadTree(zoom));
            }

            zoom = currentQuadTree.Zoom;
            for (int i = 1; i <= _updatedCurrentQuadTreeLoadFactor; i++)
            {
                zoom *= (1 / _zoomFactor);
                await Task.Run(() => GetQuadTree(zoom));
            }
        }

        private async Task GetInitialZoomQuadTreesAsync()
        {
            float zoom1 = 1 * _zoomFactor * _zoomFactor;
            float zoom2 = 1 / (_zoomFactor * _zoomFactor);

            for (int i = 1; i <= _asyncInitialLoadFactor; i++)
            {
                await Task.Run(() => GetQuadTree(zoom1));
                zoom1 *= _zoomFactor;
            }

            for (int i = 1; i <= _asyncInitialLoadFactor; i++)
            {
                await Task.Run(() => GetQuadTree(zoom2));
                zoom2 *= (1 / _zoomFactor);
            }
        }

        /// <summary>
        /// Calculates and sets the maximum values for zoom and size based on the render target's dimensions and the maximum bitmap size.
        /// </summary>
        public QuadTree GetMaxSizeQuadTree(float zoom)
        {
            //MaxZoomBitmap?.Dispose();

            Size2F maxSize;
            float maxZoom;
            Size2F maxDpi;
            RawRectangleF maxRect;

            if (_renderTarget.Size.Width > _renderTarget.Size.Height)
            {
                maxSize = new(_resCache.MaxBitmapSize, _resCache.MaxBitmapSize * (_renderTarget.Size.Height / _renderTarget.Size.Width));
                maxZoom = maxSize.Height / _renderTarget.Size.Height;
                maxDpi = new(96.0f * maxZoom, 96.0f * maxZoom);
                maxRect = new(0, 0, _renderTarget.Size.Width * maxZoom, _renderTarget.Size.Height * maxZoom);
            }
            else
            {
                maxSize = new(_resCache.MaxBitmapSize, _resCache.MaxBitmapSize * (_renderTarget.Size.Width / _renderTarget.Size.Height));
                maxZoom = maxSize.Width / _renderTarget.Size.Width;
                maxDpi = new(96.0f * maxZoom, 96.0f * maxZoom);
                maxRect = new(0, 0, _renderTarget.Size.Width * maxZoom, _renderTarget.Size.Height * maxZoom);
            }

            BitmapRenderTarget bitmapRenderTarget = new(_renderTarget, CompatibleRenderTargetOptions.None, maxSize)
            {
                DotsPerInch = maxDpi,
                AntialiasMode = AntialiasMode.PerPrimitive
            };
            bitmapRenderTarget.Transform = new RawMatrix3x2((float)_extentsMatrix.M11, (float)_extentsMatrix.M12, (float)_extentsMatrix.M21, (float)_extentsMatrix.M22, (float)_extentsMatrix.OffsetX, (float)_extentsMatrix.OffsetY);

            float thickness = 1.0f / (bitmapRenderTarget.Transform.M11 * zoom);

            bitmapRenderTarget.BeginDraw();
            bitmapRenderTarget.Clear(new RawColor4(0, 1, 0, 0.25f));
            _layerManager.Draw(bitmapRenderTarget, thickness);
            bitmapRenderTarget.EndDraw();

            QuadTree maxSizeQuadTree = new(_renderTarget, bitmapRenderTarget.Bitmap, maxZoom, _resCache, _maxBitmapSize, maxDpi);

            return maxSizeQuadTree;
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
                    if (QuadTrees != null)
                    {
                        foreach (var quadTree in QuadTrees.Values)
                        {
                            quadTree.Dispose();
                        }
                        QuadTrees.Clear();
                    }

                    InitialQuadTree?.Dispose();
                    // Note: CurrentQuadTree and InitialQuadTree might point to the same object, so no need to dispose CurrentQuadTree separately
                }

                // Free unmanaged resources (unmanaged objects) and override a finalizer below.
                // Set large fields to null.

                _disposed = true;
            }
        }

        // Override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~QuadTreeCache()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }
        #endregion
    }
}
