using Direct2DDXFViewer.DrawingObjects;
using Direct2DDXFViewer.Helpers;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Direct2DDXFViewer
{
    public class QuadTreeCache
    {
        #region Fields
        private DeviceContext1 _deviceContext;
        private ObjectLayerManager _layerManager;
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
        public float SizeLimit { get; set; }
        public int BitmapReuseFactor { get; set; }
        public float ZoomFactor { get; set; }
        public int ZoomPrecision { get; set; }
        public RawMatrix3x2 ExtentsMatrix { get; set; }
        public Rect DestRect { get; set; }
        public int Levels { get; set; }
        #endregion

        #region Constructors
        public QuadTreeCache(DeviceContext1 deviceContext, int zoomStepUpperLimit, int zoomStepLowerLimit, float sizeLimit, int bitmapReuseFactor, float zoomFactor, int zoomPrecision, RawMatrix3x2 extentsMatrix, int levels)
        {
            _deviceContext = deviceContext;
            ZoomStepUpperLimit = zoomStepUpperLimit;
            ZoomStepLowerLimit = zoomStepLowerLimit;
            SizeLimit = sizeLimit;
            BitmapReuseFactor = bitmapReuseFactor;
            ZoomFactor = zoomFactor;
            ZoomPrecision = zoomPrecision;
            ExtentsMatrix = extentsMatrix;
            DestRect = new(0, 0, _deviceContext.Size.Width, _deviceContext.Size.Height);
            Levels = levels;

            InitializeQuadTrees();
        }
        #endregion

        #region Methods
        public void InitializeQuadTrees()
        {
            // Initialize base view first
            
        }

        public void AddQuadTree(int zoomStep)
        {
            float zoom = MathHelpers.GetZoom(ZoomFactor, zoomStep, ZoomPrecision);
            Size2F size = new((float)(_deviceContext.Size.Width * zoom), (float)(_deviceContext.Size.Height * zoom));
            QuadTree quadTree = new(_deviceContext, _layerManager, size, ExtentsMatrix, DestRect, Levels, zoomStep, zoom);
            QuadTrees.Add(zoomStep, quadTree);
        }
        #endregion
    }
}
