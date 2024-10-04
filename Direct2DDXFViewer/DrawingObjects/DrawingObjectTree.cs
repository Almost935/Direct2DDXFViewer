using Direct2DControl;
using Direct2DDXFViewer.BitmapHelpers;
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

namespace Direct2DDXFViewer.DrawingObjects
{
    public class DrawingObjectTree
    {
        #region Fields
        private const float _viewInflationFactor = 1.1f;

        private ObjectLayerManager _layerManager;
        #endregion

        #region Properties
        public List<DrawingObject> DrawingObjects { get; set; } = [];
        public Rect Extents { get; set; }
        public int Levels { get; set; }
        public DrawingObjectNode Root { get; set; }
        public List<DrawingObjectNode> CurrentlyVisibleNodes { get; set; } = new();

        /// <summary>
        /// Consists of all the 0 level nodes in the tree.
        /// </summary>
        public List<DrawingObjectNode> BaseLevelNodes { get; set; } = new();
        #endregion

        #region Constructors
        public DrawingObjectTree(ObjectLayerManager layerManager, Rect extents, int levels)
        {
            _layerManager = layerManager;
            Extents = extents;
            Levels = levels;

            Initialize();
        }
        #endregion

        #region Methods
        private void Initialize()
        {
            GetDrawingObjects();
            GetRoot();
        }
        private void GetRoot()
        {
            Root = new(DrawingObjects, Levels, Extents, this);
        }
        private void GetDrawingObjects()
        {
            foreach (var layer in _layerManager.Layers.Values)
            {
                DrawingObjects.AddRange(layer.DrawingObjects);
            }
        }
        public List<DrawingObjectNode> GetIntersectingNodes(Rect view)
        {
            List<DrawingObjectNode> quadTreeNodes = [];

            quadTreeNodes.AddRange(Root.GetIntersectingQuadTreeNodes(view));

            return quadTreeNodes;
        }
        public List<DrawingObjectNode> GetIntersectingNodes(Point p)
        {
            List<DrawingObjectNode> quadTreeNodes = [];

            quadTreeNodes.AddRange(Root.GetNodeAtPoint(p));

            return quadTreeNodes;
        }

        //public void UpdateCurrentlyVisibleNodes(Rect view)
        //{
        //    view.Inflate(new System.Windows.Size);
        //    CurrentlyVisibleNodes = GetIntersectingNodes(view);
        //}
        #endregion
    }
}
