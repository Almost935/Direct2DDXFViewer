using Direct2DDXFViewer.DrawingObjects;
using Direct2DDXFViewer.Helpers;
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
    public class QuadTreeCache
    {
        #region Fields
        private Factory1 _factory;
        private DeviceContext1 _deviceContext;
        private ObjectLayerManager _layerManager;
        private int _bitmapReuseFactor;
        private int _maxBitmapSize;
        #endregion

        #region Properties
        /// <summary>
        /// Dictionary of QuadTrees with keys corresponding to zoom steps as ints.
        /// </summary>
        public Dictionary<int, QuadTree> QuadTrees { get; set; } = new();
        /// <summary>
        /// The upper limit of zoom steps. 
        /// </summary>
        public int ZoomStepUpperLimit { get; set; }
        /// <summary>
        /// The lower limit of zoom steps.
        /// </summary>
        public int ZoomStepLowerLimit { get; set; }
        /// <summary>
        /// The largets dimensions any one side of a QuadTreeNode can be.
        /// </summary>
        public float ZoomFactor { get; set; }
        public int ZoomPrecision { get; set; }
        public RawMatrix3x2 ExtentsMatrix { get; set; }
        public Rect OverallBounds { get; set; }
        public Rect OverallDestRect { get; set; }
        public int Levels { get; set; }
        public QuadTree BaseQuadTree { get; set; }
        #endregion

        #region Constructors
        public QuadTreeCache(Factory1 factory, DeviceContext1 deviceContext, ObjectLayerManager layerManager, int zoomStepUpperLimit, int zoomStepLowerLimit, int maxBitmapSize, int bitmapReuseFactor, float zoomFactor, int zoomPrecision, RawMatrix3x2 extentsMatrix, Rect overallBounds, int levels)
        {
            _factory = factory;
            _deviceContext = deviceContext;
            _layerManager = layerManager;
            ZoomStepUpperLimit = zoomStepUpperLimit;
            ZoomStepLowerLimit = zoomStepLowerLimit;
            _maxBitmapSize = maxBitmapSize;
            _bitmapReuseFactor = bitmapReuseFactor;
            ZoomFactor = zoomFactor;
            ZoomPrecision = zoomPrecision;
            ExtentsMatrix = extentsMatrix;
            OverallBounds = overallBounds;
            OverallDestRect = new(0, 0, _deviceContext.Size.Width, _deviceContext.Size.Height);
            Levels = levels;

            InitializeQuadTrees();
        }
        #endregion

        #region Methods
        public void InitializeQuadTrees()
        {
            var stopwatch = Stopwatch.StartNew();

            // Initialize base view first
            BaseQuadTree = AddQuadTree(0);

            for (int i = 0; i < ZoomStepUpperLimit; i++)
            {
                if ((i + 1) % _bitmapReuseFactor == 0)
                {
                    AddQuadTree(i + 1);
                }
            }

            stopwatch.Stop();
            Debug.WriteLine($"InitializeQuadTrees took {stopwatch.ElapsedMilliseconds} ms");
        }

        private QuadTree AddQuadTree(int zoomStep)
        {
            var stopwatch = Stopwatch.StartNew();

            float zoom = MathHelpers.GetZoom(ZoomFactor, zoomStep, ZoomPrecision);
            Size2F size = new((float)(_deviceContext.Size.Width * zoom), (float)(_deviceContext.Size.Height * zoom));
            QuadTree quadTree = new(_factory, _deviceContext, _layerManager, size, ExtentsMatrix, OverallBounds, OverallDestRect, Levels, zoomStep, zoom, _maxBitmapSize);
            QuadTrees.TryAdd(zoomStep, quadTree);

            stopwatch.Stop();
            Debug.WriteLine($"AddQuadTree for zoomStep {zoomStep} took {stopwatch.ElapsedMilliseconds} ms");

            return quadTree;
        }

        public bool TryGetQuadTree(int zoomStep, out QuadTree quadTree)
        {
            zoomStep = AdjustZoomStep(zoomStep);

            return QuadTrees.TryGetValue(zoomStep, out quadTree);
        }

        private int AdjustZoomStep(int zoomStep)
        {
            while (zoomStep % _bitmapReuseFactor != 0)
            {
                zoomStep -= 1;
            }
            return zoomStep;
        }
        #endregion
    }
}
