using Direct2DControl;
using Direct2DDXFViewer.BitmapHelpers;
using Direct2DDXFViewer.DrawingObjects;
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
        ObjectLayerManager _layerManager;
        #endregion

        #region Properties
        public List<DrawingObject> DrawingObjects { get; set; } = new();
        public Rect Bounds { get; set; }
        public int Levels { get; set; }
        public QuadTreeNode Root { get; set; }
        #endregion

        #region Constructors
        public QuadTree(ObjectLayerManager layerManager, Rect bounds, int levels)
        {
            _layerManager = layerManager;
            Bounds = bounds;
            Levels = levels;

            Initialize();
        }
        #endregion

        #region Methods
        private void Initialize()
        {
            GetDrawingObjects();
            Root = new(DrawingObjects, Bounds, Levels); 
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
