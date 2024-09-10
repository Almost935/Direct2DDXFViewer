using Direct2DControl;
using Direct2DDXFViewer.BitmapHelpers;
using Direct2DDXFViewer.DrawingObjects;
using Direct2DDXFViewer.Helpers;
using netDxf.Tables;
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
    public class QuadTree
    {
        #region Fields
        private DeviceContext1 _deviceContext;
        private ObjectLayerManager _layerManager;
        #endregion

        #region Properties
        public List<DrawingObject> DrawingObjects { get; set; } = new();
        public RawMatrix3x2 ExtentsMatrix { get; set; }
        public Rect DestRect { get; set; }
        public Size2F OverallSize { get; set; }
        public int Levels { get; set; }
        public QuadTreeNode Root { get; set; }
        public int ZoomStep { get; set; }
        public float Zoom { get; set; }
        #endregion

        #region Constructors
        public QuadTree(DeviceContext1 deviceContext, ObjectLayerManager layerManager, Size2F overallSize, RawMatrix3x2 extentsMatrix, Rect destRect, int levels, int zoomStep, float zoom)
        {
            _deviceContext = deviceContext;
            _layerManager = layerManager;
            OverallSize = overallSize;
            ExtentsMatrix = extentsMatrix;
            DestRect = destRect;
            Levels = levels;
            ZoomStep = zoomStep;
            

            Initialize();
        }
        #endregion

        #region Methods
        private void Initialize()
        {
            GetDrawingObjects();
            Root = new(_deviceContext, DrawingObjects, ExtentsMatrix, DestRect, OverallSize, Levels); 
        }
        private void GetDrawingObjects()
        {
            foreach (var layer in _layerManager.Layers.Values)
            {
                DrawingObjects.AddRange(layer.DrawingObjects);
            }
        }
        public List<QuadTreeNode> GetIntersectingNodes(Rect view)
        {
            List<QuadTreeNode> quadTreeNodes = [];

            quadTreeNodes.AddRange(Root.GetIntersectingQuadTreeNodes(view));

            return quadTreeNodes;
        }
        public List<QuadTreeNode> GetIntersectingNodes(Point p)
        {
            List<QuadTreeNode> quadTreeNodes = [];
            quadTreeNodes.AddRange(Root.GetNodeAtPoint(p));

            return quadTreeNodes;
        }
        #endregion
    }
}
