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
using System.Runtime.CompilerServices;
using System.Drawing;
using System.Security.Policy;

namespace Direct2DDXFViewer
{
    public class QuadTreeCache : IDisposable
    {
        #region Fields
        public enum QuadTreeGetterType { New, Existing };

        private RenderTarget _renderTarget;
        private Size2F _renderTargetSize;
        private Factory1 _factory;
        private ObjectLayerManager _layerManager;
        private Matrix _extentsMatrix;
        private ResourceCache _resCache;
        private readonly float _zoomFactor;
        private int _initialLoadFactor = 5;
        private int _updatedCurrentQuadTreeLoadFactor = 1;
        private int _asyncInitialLoadFactor = 9;
        private bool _disposed = false;

        private const float _maxBitmapSize = 10000;
        #endregion

        #region Properties 
        public ConcurrentDictionary<double, List<QuadTree>> QuadTrees { get; private set; }
        public List<QuadTree> CurrentQuadTrees { get; private set; }
        public List<QuadTree> InitialQuadTrees { get; private set; }
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
            InitialQuadTrees = GetQuadTree(1).quadTrees;
            CurrentQuadTrees = InitialQuadTrees;

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
                CurrentQuadTrees = InitialQuadTrees;
            }
            else
            {
                var tup = GetQuadTree(zoom);
                CurrentQuadTrees = tup.quadTrees;
                var info = tup.info;
                
                if (info == QuadTreeGetterType.New)
                {
                    //GetAdjacentZoomQuadTreesAsync(CurrentQuadTrees);
                }
            }
        }

        /// <summary>
        /// Gets the quad tree for a given zoom level.
        /// </summary>
        /// <param name="zoom">The zoom level.</param>
        /// <returns>The quad tree for the specified zoom level.</returns>
        public (List<QuadTree> quadTrees, QuadTreeGetterType info) GetQuadTree(float zoom)
        {
            // If the zoom is already in the dictionary, return the quad tree
            if (QuadTrees.TryGetValue(Math.Round(zoom, 3), out List<QuadTree> quadTrees))
            {
                return (quadTrees, QuadTreeGetterType.Existing);
            }

            Size2F size = new(_renderTargetSize.Width * zoom, _renderTargetSize.Height * zoom);

            // Check if resulting zoomed bitmap is larger than maximum allowable BitmapRenderTarget size
            if (size.Width > _resCache.MaxBitmapSize ||
                size.Height > _resCache.MaxBitmapSize)
            {
                Debug.WriteLine($"CreateOversizedQuadTrees: {zoom}");
                quadTrees = CreateOversizedQuadTrees(_renderTarget, zoom, size, _extentsMatrix, _resCache.MaxBitmapSize);
                return (quadTrees, QuadTreeGetterType.New);
            }

            quadTrees = new List<QuadTree>() { CreateQuadTree(_renderTarget, zoom, size, _extentsMatrix) };
            QuadTrees.TryAdd(Math.Round((double)zoom, 3), quadTrees);
            return (quadTrees, QuadTreeGetterType.New);
        }

        private QuadTree CreateQuadTree(RenderTarget renderTarget, float zoom, Size2F size, Matrix extentsMatrix)
        {
            Debug.WriteLine($"zoom: {zoom}");

            Size2F dpi = new(96.0f * zoom, 96.0f * zoom);
            BitmapRenderTarget bitmapRenderTarget = new(renderTarget, CompatibleRenderTargetOptions.None, size)
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

            QuadTree quadTree = new(renderTarget, bitmapRenderTarget.Bitmap, zoom, _maxBitmapSize, dpi);

            return quadTree;
        }

        private List<QuadTree> CreateOversizedQuadTrees(RenderTarget renderTarget, float zoom, Size2F fullSize, Matrix extentsMatrix, int maxBitmapRenderTargetSize)
        {
            List<QuadTree> quadTrees = new();
            
            Size2F dpi = new(96.0f * zoom, 96.0f * zoom);

            if (fullSize.Width > fullSize.Height)
            {
                int numTilesWidth = (int)Math.Ceiling(fullSize.Width / maxBitmapRenderTargetSize);
                int numTilesHeight = (int)Math.Ceiling(fullSize.Height / maxBitmapRenderTargetSize);
                float width = fullSize.Width / numTilesWidth;
                float height = fullSize.Height / numTilesHeight;

                for (int i = 0; i < numTilesWidth; i++)
                {
                    for (int j = 0; j < numTilesHeight; j++)
                    {
                        Rect bounds = new(i * width, j * height, width, height);
                        Size2F size = new(width, height);

                        BitmapRenderTarget bitmapRenderTarget = new(_renderTarget, CompatibleRenderTargetOptions.None, size)
                        {
                            DotsPerInch = dpi,
                            AntialiasMode = AntialiasMode.PerPrimitive
                        };
                        bitmapRenderTarget.BeginDraw();
                        bitmapRenderTarget.Clear(new RawColor4(0, 1, 0, 0.25f));
                        bitmapRenderTarget.Transform = new RawMatrix3x2((float)extentsMatrix.M11, (float)extentsMatrix.M12, (float)extentsMatrix.M21, (float)extentsMatrix.M22, (float)extentsMatrix.OffsetX + (width * i), (float)extentsMatrix.OffsetY + (height * j));

                        float thickness = 1.0f / (bitmapRenderTarget.Transform.M11 * zoom);
                        _layerManager.Draw(bitmapRenderTarget, thickness);
                        bitmapRenderTarget.EndDraw();
                        QuadTree quadTree = new(_renderTarget, bitmapRenderTarget.Bitmap, zoom, _maxBitmapSize, dpi);

                        quadTrees.Add(quadTree);
                    }
                }
            }
            else
            {

            }

            return quadTrees;
        }

        /// <summary>
        /// Verify's that the QuadTree's adjacent to the given QuadTree are already cached and, if not, created.
        /// </summary>
        /// <param name="currentQuadTrees">The QuadTree from which the adjacent QuadTree's will be verified</param>
        /// <returns></returns>
        private async Task GetAdjacentZoomQuadTreesAsync(List<QuadTree> currentQuadTrees)
        {
            //if (currentQuadTrees.Count == 0) { return; }

            //float zoom = currentQuadTrees.First().Zoom;
            //for (int i = 1; i <= _updatedCurrentQuadTreeLoadFactor; i++)
            //{
            //    zoom *= _zoomFactor;
            //    await Task.Run(() => GetQuadTree(zoom));
            //}

            //zoom = currentQuadTrees.First().Zoom;
            //for (int i = 1; i <= _updatedCurrentQuadTreeLoadFactor; i++)
            //{
            //    zoom *= (1 / _zoomFactor);
            //    await Task.Run(() => GetQuadTree(zoom));
            //}
        }

        private async Task GetInitialZoomQuadTreesAsync()
        {
            float zoom1 = 1 * (float)(Math.Pow(_zoomFactor, _initialLoadFactor));
            float zoom2 = 1 / (float)(Math.Pow(_zoomFactor, _initialLoadFactor));

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
        //public QuadTree GetMaxSizeQuadTree(float zoom)
        //{
        //    //MaxZoomBitmap?.Dispose();

        //    Size2F maxSize;
        //    float maxZoom;
        //    Size2F maxDpi;
        //    RawRectangleF maxRect;

        //    if (_renderTarget.Size.Width > _renderTarget.Size.Height)
        //    {
        //        maxSize = new(_resCache.MaxBitmapSize, _resCache.MaxBitmapSize * (_renderTarget.Size.Height / _renderTarget.Size.Width));
        //        maxZoom = maxSize.Height / _renderTarget.Size.Height;
        //        maxDpi = new(96.0f * maxZoom, 96.0f * maxZoom);
        //        maxRect = new(0, 0, _renderTarget.Size.Width * maxZoom, _renderTarget.Size.Height * maxZoom);
        //    }
        //    else
        //    {
        //        maxSize = new(_resCache.MaxBitmapSize, _resCache.MaxBitmapSize * (_renderTarget.Size.Width / _renderTarget.Size.Height));
        //        maxZoom = maxSize.Width / _renderTarget.Size.Width;
        //        maxDpi = new(96.0f * maxZoom, 96.0f * maxZoom);
        //        maxRect = new(0, 0, _renderTarget.Size.Width * maxZoom, _renderTarget.Size.Height * maxZoom);
        //    }

        //    BitmapRenderTarget bitmapRenderTarget = new(_renderTarget, CompatibleRenderTargetOptions.None, maxSize)
        //    {
        //        DotsPerInch = maxDpi,
        //        AntialiasMode = AntialiasMode.PerPrimitive
        //    };
        //    bitmapRenderTarget.Transform = new RawMatrix3x2((float)_extentsMatrix.M11, (float)_extentsMatrix.M12, (float)_extentsMatrix.M21, (float)_extentsMatrix.M22, (float)_extentsMatrix.OffsetX, (float)_extentsMatrix.OffsetY);

        //    float thickness = 1.0f / (bitmapRenderTarget.Transform.M11 * zoom);

        //    bitmapRenderTarget.BeginDraw();
        //    bitmapRenderTarget.Clear(new RawColor4(0, 1, 0, 0.25f));
        //    _layerManager.Draw(bitmapRenderTarget, thickness);
        //    bitmapRenderTarget.EndDraw();

        //    QuadTree maxSizeQuadTree = new(_renderTarget, bitmapRenderTarget.Bitmap, maxZoom, _resCache, _maxBitmapSize, maxDpi);

        //    return maxSizeQuadTree;
        //}

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
                    if (QuadTrees != null)
                    {
                        foreach (var quadTrees in QuadTrees.Values)
                        {
                            foreach (var quadTree in quadTrees)
                            {
                                quadTree.Dispose();
                            }
                        }
                        QuadTrees.Clear();
                    }

                    foreach (var quadTree in InitialQuadTrees)
                    {
                        quadTree.Dispose();
                    }

                    _renderTarget?.Dispose();
                    _factory?.Dispose();
                    _layerManager?.Dispose();
                    _resCache?.Dispose();
                }

                // Free unmanaged resources (if any)

                _disposed = true;
            }
        }
        ~QuadTreeCache()
        {
            Dispose(false);
        }
        #endregion
    }
}
