using Direct2DDXFViewer.DrawingObjects;
using Direct2DDXFViewer.Helpers;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Direct2DDXFViewer
{
    public class QuadTreeCache
    {
        #region Fields
        private readonly Factory1 _factory;
        private readonly DeviceContext1 _deviceContext;
        private readonly ObjectLayerManager _layerManager;
        private readonly int _bitmapReuseFactor;
        private readonly int _loadedQuadTreesFactor;
        private readonly int _initializedQuadTreeFactor;
        private readonly int _maxBitmapSize;
        private string _tempFolderPath;
        private QuadTree _baseQuadTree;
        private QuadTree[] _adjacentZoomedInQuadTrees;
        private QuadTree[] _adjacentZoomedOutQuadTrees;
        private bool _adjacentQuadTreesIsDirty = true;
        private int _maxZoomStep;
        private int _minZoomStep;

        ///// <summary>
        ///// Represents the zoom step QuadTree that is one level more zoomed in than the current one.
        ///// </summary>
        //private QuadTree _nextZoomStepQuadTree;

        ///// <summary>
        ///// Represents the zoom step QuadTree that is one level more zoomed out than the current one.
        ///// </summary>
        //private QuadTree _prevZoomStepQuadTree;
        #endregion

        #region Properties
        /// <summary>
        /// Dictionary of QuadTrees with keys corresponding to zoom steps as ints.
        /// </summary>
        public Dictionary<int, QuadTree> QuadTrees { get; set; } = new();

        /// <summary>
        /// The largest dimension any one side of a QuadTreeNode can be.
        /// </summary>
        public float ZoomFactor { get; set; }
        public int ZoomPrecision { get; set; }
        public RawMatrix3x2 ExtentsMatrix { get; set; }
        public Rect OverallBounds { get; set; }
        public Rect OverallDestRect { get; set; }
        public QuadTree CurrentQuadTree { get; set; }
        #endregion

        #region Constructors
        public QuadTreeCache(Factory1 factory, DeviceContext1 deviceContext, ObjectLayerManager layerManager, int loadedQuadTreesFactor, int initializedQuadTreeFactor, int maxBitmapSize, int bitmapReuseFactor, float zoomFactor, int zoomPrecision, RawMatrix3x2 extentsMatrix, Rect overallBounds)
        {
            _factory = factory;
            _deviceContext = deviceContext;
            _layerManager = layerManager;
            _loadedQuadTreesFactor = loadedQuadTreesFactor;
            _initializedQuadTreeFactor = initializedQuadTreeFactor;

            _maxZoomStep = initializedQuadTreeFactor * bitmapReuseFactor;
            _minZoomStep = 0;

            _maxBitmapSize = maxBitmapSize;
            _bitmapReuseFactor = bitmapReuseFactor;
            ZoomFactor = zoomFactor;
            ZoomPrecision = zoomPrecision;
            ExtentsMatrix = extentsMatrix;
            OverallBounds = overallBounds;
            OverallDestRect = new(0, 0, _deviceContext.Size.Width, _deviceContext.Size.Height);

            CreateTempFolder();
            InitializeQuadTrees();
            RunInitializeQuadTreesAsync();
            RunGetAdjacentQuadTreesAsync();
        }
        #endregion

        #region Methods
        private void InitializeQuadTrees()
        {
            var stopwatch = Stopwatch.StartNew();
            _baseQuadTree = AddQuadTree(0);
            CurrentQuadTree = _baseQuadTree;

            _adjacentZoomedInQuadTrees = new QuadTree[_loadedQuadTreesFactor];
            _adjacentZoomedOutQuadTrees = new QuadTree[_loadedQuadTreesFactor];

            for (int i = 0; i < _loadedQuadTreesFactor; i++)
            {
                QuadTree quadTree = AddQuadTree((i + 1) * _bitmapReuseFactor);
                if (quadTree is not null)
                {
                    _adjacentZoomedInQuadTrees[i] = quadTree;
                }

                Debug.WriteLine($"InitializeActiveQuadTrees, Zoom Step: {i * _bitmapReuseFactor}");
            }

            stopwatch.Stop();
            Debug.WriteLine($"InitializeQuadTrees took {stopwatch.ElapsedMilliseconds} ms");
        }

        private async void RunInitializeQuadTreesAsync()
        {
            InitializeQuadTreesAsync();
        }
        private void InitializeQuadTreesAsync()
        {
            for (int i = 0; i < _initializedQuadTreeFactor; i++)
            {
                if (i > _loadedQuadTreesFactor)
                {
                    var quadTree = AddQuadTree(i * _bitmapReuseFactor);
                    quadTree.DisposeBitmaps();

                    Debug.WriteLine($"InitializeActiveQuadTrees, Zoom Step: {i * _bitmapReuseFactor}");
                }
            }
        }

        private QuadTree AddQuadTree(int zoomStep)
        {
            zoomStep = AdjustZoomStep(zoomStep);

            if (!QuadTrees.ContainsKey(zoomStep)) ;

            float zoom = MathHelpers.GetZoom(ZoomFactor, zoomStep, ZoomPrecision);
            Size2F size = new((float)(_deviceContext.Size.Width * zoom), (float)(_deviceContext.Size.Height * zoom));
            QuadTree quadTree = new(_factory, _deviceContext, _layerManager, size, ExtentsMatrix, OverallBounds, OverallDestRect, zoomStep, zoom, _maxBitmapSize, _tempFolderPath);
            var added = QuadTrees.TryAdd(zoomStep, quadTree);

            if (!added)
            {
                quadTree.Dispose();
                throw new Exception("Failed to add QuadTree to QuadTrees dictionary.");
            }

            return quadTree;
        }

        public bool TryGetQuadTree(int zoomStep, out QuadTree quadTree)
        {
            zoomStep = AdjustZoomStep(zoomStep);

            quadTree = _adjacentZoomedInQuadTrees.FirstOrDefault(qt => qt.ZoomStep == zoomStep);
            if (quadTree is not null)
            {
                return true;
            }

            quadTree = _adjacentZoomedOutQuadTrees.FirstOrDefault(qt => qt is not null && qt.ZoomStep == zoomStep);
            if (quadTree is not null)
            {
                return true;
            }

            return QuadTrees.TryGetValue(zoomStep, out quadTree);
        }

        public void UpdateZoomStep(int zoomStep)
        {
            zoomStep = AdjustZoomStep(zoomStep);
            TryGetQuadTree(zoomStep, out QuadTree quadTree);
            CurrentQuadTree = quadTree;

            _adjacentQuadTreesIsDirty = true;
        }

        private async Task RunGetAdjacentQuadTreesAsync()
        {
            while (true)
            {
                await Task.Run(() => GetAdjacentQuadTreesAsync());
                await Task.Delay(100);
            }
        }
        private void GetAdjacentQuadTreesAsync()
        {
            if (!_adjacentQuadTreesIsDirty)
            {
                return;
            }

            int currentStep = CurrentQuadTree.ZoomStep;

            for (int i = 0; i < _loadedQuadTreesFactor; i++)
            {
                int upperStep = AdjustZoomStep(currentStep + (i + 1) * _bitmapReuseFactor);
                int lowerStep = AdjustZoomStep(currentStep - (i + 1) * _bitmapReuseFactor);

                if (upperStep <= _maxZoomStep)
                {
                    bool found = TryGetQuadTree(upperStep, out _adjacentZoomedInQuadTrees[i]);
                    if (found)
                    {
                        if (!_adjacentZoomedInQuadTrees[i].BitmapsLoaded)
                        {
                            _adjacentZoomedInQuadTrees[i].LoadBitmaps();
                        }
                    }
                }

                if (lowerStep >= _minZoomStep)
                {
                    bool found = TryGetQuadTree(lowerStep, out _adjacentZoomedOutQuadTrees[i]);
                    if (found)
                    {
                        if (!_adjacentZoomedOutQuadTrees[i].BitmapsLoaded)
                        {
                            _adjacentZoomedOutQuadTrees[i].LoadBitmaps();
                        }
                    }
                }
            }

            _adjacentQuadTreesIsDirty = false;
        }

        /// <summary>
        /// Adjusts a zoom step to be divisible by the bitmap reuse factor if the zoom step is greater than 0. If the zoom step is less than 0, it will return 0.
        /// </summary>
        /// <param name="zoomStep">An integer representing the zoom step that is to be adjusted.</param>
        /// <returns></returns>
        private int AdjustZoomStep(int zoomStep)
        {
            if (zoomStep <= _minZoomStep)
            {
                return _minZoomStep;
            }

            while (zoomStep % _bitmapReuseFactor != 0)
            {
                zoomStep -= 1;
            }
            return zoomStep;
        }

        private void CreateTempFolder()
        {
            _tempFolderPath = Path.Combine(Path.GetTempPath(), "CadViewer");

            if (Directory.Exists(_tempFolderPath))
            {
                Directory.Delete(_tempFolderPath, true);
            }
            Directory.CreateDirectory(_tempFolderPath);
        }
        #endregion
    }
}
