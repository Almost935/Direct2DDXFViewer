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
        private int _initialLoadFactor = 15;
        private int _updatedCurrentQuadTreeLoadFactor = 2;
        private int _asyncInitialLoadFactor = 9;
        private bool _disposed = false;
        private bool _backgroundThreadActive = false;

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
            //GetInitialZoomQuadTreesAsync();
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
            while (_backgroundThreadActive) { Task.Delay(100); }

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
                quadTrees = CreateOversizedQuadTrees(_renderTarget, zoom, size, _extentsMatrix, _resCache.MaxBitmapSize);
                QuadTrees.TryAdd(Math.Round((double)zoom, 3), quadTrees);
                return (quadTrees, QuadTreeGetterType.New);
            }

            quadTrees = new List<QuadTree>() { CreateQuadTree(_renderTarget, zoom, size, _extentsMatrix) };
            QuadTrees.TryAdd(Math.Round((double)zoom, 3), quadTrees);
            return (quadTrees, QuadTreeGetterType.New);
        }

        private QuadTree CreateQuadTree(RenderTarget renderTarget, float zoom, Size2F size, Matrix extentsMatrix)
        {
            Size2F dpi = new(96.0f * zoom, 96.0f * zoom);
            BitmapRenderTarget bitmapRenderTarget = new(renderTarget, CompatibleRenderTargetOptions.None, size)
            {
                DotsPerInch = dpi,
                AntialiasMode = AntialiasMode.PerPrimitive
            };

            bitmapRenderTarget.BeginDraw();
            bitmapRenderTarget.Clear(new RawColor4(0, 1, 0, 0.25f));

            RawMatrix3x2 transform = new RawMatrix3x2((float)extentsMatrix.M11, (float)extentsMatrix.M12, (float)extentsMatrix.M21, (float)extentsMatrix.M22, (float)extentsMatrix.OffsetX, (float)extentsMatrix.OffsetY);
            bitmapRenderTarget.Transform = transform;

            float thickness = 1.0f / (bitmapRenderTarget.Transform.M11 * zoom);
            _layerManager.DrawToRenderTarget(bitmapRenderTarget, thickness, Rect.Empty);
            bitmapRenderTarget.EndDraw();

            QuadTree quadTree = new(renderTarget, bitmapRenderTarget.Bitmap, zoom, _maxBitmapSize, dpi,
                new Rect(0, 0, bitmapRenderTarget.Bitmap.Size.Width, bitmapRenderTarget.Bitmap.Size.Height),
                new Rect(0, 0, renderTarget.Size.Width, renderTarget.Size.Height));

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
                float destWidth = renderTarget.Size.Width / numTilesWidth;
                float destHeight = renderTarget.Size.Height / numTilesHeight;
                float treeWidth = fullSize.Width / numTilesWidth;
                float treeHeight = fullSize.Height / numTilesHeight;

                for (int i = 0; i < numTilesWidth; i++)
                {
                    for (int j = 0; j < numTilesHeight; j++)
                    {
                        Size2F size = new(treeWidth, treeHeight);

                        try
                        {
                            BitmapRenderTarget bitmapRenderTarget = new(renderTarget, CompatibleRenderTargetOptions.None, size)
                            {
                                DotsPerInch = dpi,
                                AntialiasMode = AntialiasMode.PerPrimitive
                            };
                            bitmapRenderTarget.BeginDraw();
                            bitmapRenderTarget.Clear(new RawColor4(0, 1, 0, 0.25f));

                            RawMatrix3x2 transform = new((float)extentsMatrix.M11, (float)extentsMatrix.M12, (float)extentsMatrix.M21, (float)extentsMatrix.M22, (float)extentsMatrix.OffsetX - (destWidth * i), (float)extentsMatrix.OffsetY + (destHeight * j));

                            bitmapRenderTarget.Transform = transform;

                            float thickness = 1.0f / (bitmapRenderTarget.Transform.M11 * zoom);
                            _layerManager.DrawToRenderTarget(bitmapRenderTarget, thickness, Rect.Empty);
                            bitmapRenderTarget.EndDraw();

                            Rect destRect = new(renderTarget.Size.Width * ((float)i / numTilesWidth), renderTarget.Size.Height * ((float)j / numTilesHeight), destWidth, destHeight);

                            Rect treeRect = new(fullSize.Width * ((float)i / numTilesWidth), fullSize.Height * ((float)j / numTilesHeight), treeWidth, treeHeight);
                            QuadTree quadTree = new(_renderTarget, bitmapRenderTarget.Bitmap, zoom, _maxBitmapSize, dpi, treeRect, destRect);

                            quadTrees.Add(quadTree);
                        }
                        catch (SharpDXException ex) when (ex.ResultCode == ResultCode.RecreateTarget)
                        {

                        }
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
            if (currentQuadTrees.Count == 0) { return; }

            _backgroundThreadActive = true;

            float zoom = currentQuadTrees.First().Zoom;
            for (int i = 1; i <= _updatedCurrentQuadTreeLoadFactor; i++)
            {
                zoom *= _zoomFactor;
                await Task.Run(() => GetQuadTree(zoom));
            }

            zoom = currentQuadTrees.First().Zoom;
            for (int i = 1; i <= _updatedCurrentQuadTreeLoadFactor; i++)
            {
                zoom *= (1 / _zoomFactor);
                await Task.Run(() => GetQuadTree(zoom));
            }

            _backgroundThreadActive = false;
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
