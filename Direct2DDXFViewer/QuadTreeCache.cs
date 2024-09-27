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
        private readonly int _maxBitmapSize;
        private string _tempFolderPath;
        private QuadTree _baseQuadTree;
        private QuadTree[] _activeQuadTrees = new QuadTree[4];
        #endregion

        #region Properties
        /// <summary>
        /// Dictionary of QuadTrees with keys corresponding to zoom steps as ints.
        /// </summary>
        public Dictionary<int, QuadTree> QuadTrees { get; set; } = new();
        /// <summary>
        /// The total number of QuadTree's that are initialized. If they are not within a 
        /// </summary>
        public int QuadTreeInitializationFactor { get; set; }
        /// <summary>
        /// The upper limit of zoom steps. 
        /// </summary>
        public int ActiveQuadTreeFactor { get; set; }
        ///// <summary>
        ///// The lower limit of zoom steps.
        ///// </summary>
        //public int ZoomStepLowerLimit { get; set; }
        /// <summary>
        /// The largets dimensions any one side of a QuadTreeNode can be.
        /// </summary>
        public float ZoomFactor { get; set; }
        public int ZoomPrecision { get; set; }
        public RawMatrix3x2 ExtentsMatrix { get; set; }
        public Rect OverallBounds { get; set; }
        public Rect OverallDestRect { get; set; }
        public QuadTree CurrentQuadTree { get; set; }
        #endregion

        #region Constructors
        public QuadTreeCache(Factory1 factory, DeviceContext1 deviceContext, ObjectLayerManager layerManager, int quadTreeInitializationFactor, int activeQuadTreeFactor, int maxBitmapSize, int bitmapReuseFactor, float zoomFactor, int zoomPrecision, RawMatrix3x2 extentsMatrix, Rect overallBounds)
        {
            _factory = factory;
            _deviceContext = deviceContext;
            _layerManager = layerManager;
            QuadTreeInitializationFactor = quadTreeInitializationFactor;
            ActiveQuadTreeFactor = activeQuadTreeFactor;
            _maxBitmapSize = maxBitmapSize;
            _bitmapReuseFactor = bitmapReuseFactor;
            ZoomFactor = zoomFactor;
            ZoomPrecision = zoomPrecision;
            ExtentsMatrix = extentsMatrix;
            OverallBounds = overallBounds;
            OverallDestRect = new(0, 0, _deviceContext.Size.Width, _deviceContext.Size.Height);

            CreateTempFolder();     
            InitializeActiveQuadTrees();
            RunInitializeQuadTreesAsync();
        }
        #endregion

        #region Methods
        private void InitializeActiveQuadTrees()
        {
            var stopwatch = Stopwatch.StartNew();

            // Initialize base view first
            _baseQuadTree = AddQuadTree(0);
            CurrentQuadTree = _baseQuadTree;

            for (int i = 0; i < ActiveQuadTreeFactor; i++)
            {
                if ((i + 1) % _bitmapReuseFactor == 0)
                {
                    AddQuadTree(i + 1);
                }
            }

            stopwatch.Stop();
            Debug.WriteLine($"InitializeQuadTrees took {stopwatch.ElapsedMilliseconds} ms");
        }

        private async void RunInitializeQuadTreesAsync()
        {
            InitializeQuadTrees();
        }
        private void InitializeQuadTrees()
        {
            int count = 0;

            while (count < QuadTreeInitializationFactor)
            {
                if (count > ActiveQuadTreeFactor)
                {
                    AddQuadTree(count * _bitmapReuseFactor);
                }

                count += _bitmapReuseFactor;
            }
        }

        private async void RunUpdateActiveQuadTreesAsync()
        {
            while (true) 
            {
                
            }
        }
        private void UpdateActiveQuadTrees()
        {

        }

        private QuadTree AddQuadTree(int zoomStep)
        {
            zoomStep = AdjustZoomStep(zoomStep);

            if (!QuadTrees.ContainsKey(zoomStep)) ;

            float zoom = MathHelpers.GetZoom(ZoomFactor, zoomStep, ZoomPrecision);
            Size2F size = new((float)(_deviceContext.Size.Width * zoom), (float)(_deviceContext.Size.Height * zoom));
            QuadTree quadTree = new(_factory, _deviceContext, _layerManager, size, ExtentsMatrix, OverallBounds, OverallDestRect, zoomStep, zoom, _maxBitmapSize, _tempFolderPath);
            QuadTrees.TryAdd(zoomStep, quadTree);

            return quadTree;
        }

        public bool TryGetQuadTree(int zoomStep, out QuadTree quadTree)
        {
            zoomStep = AdjustZoomStep(zoomStep);

            return QuadTrees.TryGetValue(zoomStep, out quadTree);
        }
        
        public void UpdateZoomStep(int zoomStep)
        {
            zoomStep = AdjustZoomStep(zoomStep);
            TryGetQuadTree(zoomStep, out QuadTree quadTree);
            CurrentQuadTree = quadTree;
        }

        private int AdjustZoomStep(int zoomStep)
        {
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
