using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using Direct2DControl;
using System.Xml.Linq;
using System.Windows.Media;
using Direct2DDXFViewer.DrawingObjects;
using SharpDX.Direct3D11;
using netDxf;
using Direct2DDXFViewer.Helpers;
using netDxf.Tables;

namespace Direct2DDXFViewer
{
    public class QuadTreeNode
    {
        #region Fields
        #endregion

        #region Properties
        public List<DrawingObject> DrawingObjects { get; set; } = [];
        public Rect Bounds { get; set; }
        public int Level { get; set; }
        public QuadTreeNode[] ChildNodes { get; set; }
        #endregion

        #region Constructors
        public QuadTreeNode(List<DrawingObject> drawingObjects, Rect bounds, int level)
        {
            DrawingObjects = drawingObjects;
            Bounds = bounds;
            Level = level;
            Subdivide();
        }
        #endregion

        #region Methods
        public bool NodeIntersects(Rect view)
        {
            return MathHelpers.RectsIntersect(Bounds, view);
        }
        public List<QuadTreeNode> GetIntersectingQuadTreeNodes(Rect view)
        {
            List<QuadTreeNode> intersectingNodes = new();

            if (MathHelpers.RectsIntersect(view, Bounds))
            {
                if (ChildNodes is null)
                {
                    intersectingNodes.Add(this);
                }
                else
                {
                    foreach (var child in ChildNodes)
                    {
                        intersectingNodes.AddRange(child.GetIntersectingQuadTreeNodes(view));
                    }
                }
            }
            return intersectingNodes;
        }
        public List<QuadTreeNode> GetNodeAtPoint(Point p)
        {
            List<QuadTreeNode> nodes = new();

            if (Bounds.Contains(p))
            {
                if (Level == 0)
                {
                    nodes.Add(this);
                }
                else
                {
                    foreach (var child in ChildNodes)
                    {
                        nodes.AddRange(child.GetNodeAtPoint(p));
                    }
                }
            }
            return nodes;
        }
        private void Subdivide()
        {
            if (Level > 0)
            {
                ChildNodes = new QuadTreeNode[4];
                double halfWidth = Bounds.Width / 2;
                double halfHeight = Bounds.Height / 2;

                Rect bounds1 = new(Bounds.Left, Bounds.Top, halfWidth, halfHeight);
                Rect bounds2 = new(Bounds.Left + halfWidth, Bounds.Top, halfWidth, halfHeight);
                Rect bounds3 = new(Bounds.Left, Bounds.Top + halfHeight, halfWidth, halfHeight);
                Rect bounds4 = new(Bounds.Left + halfWidth, Bounds.Top + halfHeight, halfWidth, halfHeight);

                List<DrawingObject> objects1 = [];
                List<DrawingObject> objects2 = [];
                List<DrawingObject> objects3 = [];
                List<DrawingObject> objects4 = [];

                foreach (var drawingObject in DrawingObjects)
                {
                    if (drawingObject.DrawingObjectIsInRect(bounds1))
                    {
                        objects1.Add(drawingObject);
                    }
                    if (drawingObject.DrawingObjectIsInRect(bounds2))
                    {
                        objects2.Add(drawingObject);
                    }
                    if (drawingObject.DrawingObjectIsInRect(bounds3))
                    {
                        objects3.Add(drawingObject);
                    }
                    if (drawingObject.DrawingObjectIsInRect(bounds4))
                    {
                        objects4.Add(drawingObject);
                    }
                }

                ChildNodes[0] = new QuadTreeNode(objects1, bounds1, Level - 1);
                ChildNodes[1] = new QuadTreeNode(objects2, bounds2, Level - 1);
                ChildNodes[2] = new QuadTreeNode(objects3, bounds3, Level - 1);
                ChildNodes[3] = new QuadTreeNode(objects4, bounds4, Level - 1);
            }
        }
        #endregion
    }
}

