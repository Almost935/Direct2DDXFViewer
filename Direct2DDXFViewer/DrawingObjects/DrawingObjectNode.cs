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
using netDxf;
using Direct2DDXFViewer.Helpers;
using netDxf.Tables;
using Direct2DDXFViewer.BitmapHelpers;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class DrawingObjectNode
    {
        #region Fields
        #endregion

        #region Properties
        public List<DrawingObject> DrawingObjects { get; set; } = [];
        public Rect Extents { get; set; }
        public int Level { get; set; }
        public DrawingObjectNode[] ChildNodes { get; set; }
        public DrawingObjectTree Tree { get; set; }
        #endregion

        #region Constructors
        public DrawingObjectNode(List<DrawingObject> drawingObjects, int level, Rect extents, DrawingObjectTree tree)
        {
            DrawingObjects = drawingObjects;
            Level = level;
            Extents = extents;
            Tree = tree;

            if (Level == 0) { Tree.BaseLevelNodes.Add(this); }

            Subdivide();
        }
        #endregion

        #region Methods
        public List<DrawingObjectNode> GetIntersectingQuadTreeNodes(Rect view)
        {
            List<DrawingObjectNode> intersectingNodes = [];

            if (MathHelpers.RectsIntersect(view, Extents))
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
        public List<DrawingObjectNode> GetNodeAtPoint(Point p)
        {
            List<DrawingObjectNode> nodes = new();

            if (Extents.Contains(p))
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
                ChildNodes = new DrawingObjectNode[4];

                // Represents which quandrant each of the 1-4 is in
                Point factor1 = new(0, 0);
                Point factor2 = new(1, 0);
                Point factor3 = new(0, 1);
                Point factor4 = new(1, 1);

                // Represents the dxf coordinate bounds of each quadrant.
                Size halfBoundsSize = new(Extents.Width / 2, Extents.Height / 2);
                Rect extents1 = new(Extents.Left + (halfBoundsSize.Width * factor1.X), Extents.Top + (halfBoundsSize.Height * factor1.Y), halfBoundsSize.Width, halfBoundsSize.Height);
                Rect extents2 = new(Extents.Left + (halfBoundsSize.Width * factor2.X), Extents.Top + (halfBoundsSize.Height * factor2.Y), halfBoundsSize.Width, halfBoundsSize.Height);
                Rect extents3 = new(Extents.Left + (halfBoundsSize.Width * factor3.X), Extents.Top + (halfBoundsSize.Height * factor3.Y), halfBoundsSize.Width, halfBoundsSize.Height);
                Rect extents4 = new(Extents.Left + (halfBoundsSize.Width * factor4.X), Extents.Top + (halfBoundsSize.Height * factor4.Y), halfBoundsSize.Width, halfBoundsSize.Height);

                List<DrawingObject> objects1 = [];
                List<DrawingObject> objects2 = [];
                List<DrawingObject> objects3 = [];
                List<DrawingObject> objects4 = [];

                foreach (var drawingObject in DrawingObjects)
                {
                    if (drawingObject.DrawingObjectIsInRect(extents1))
                    {
                        objects1.Add(drawingObject);
                    }
                    if (drawingObject.DrawingObjectIsInRect(extents2))
                    {
                        objects2.Add(drawingObject);
                    }
                    if (drawingObject.DrawingObjectIsInRect(extents3))
                    {
                        objects3.Add(drawingObject);
                    }
                    if (drawingObject.DrawingObjectIsInRect(extents4))
                    {
                        objects4.Add(drawingObject);
                    }
                }

                ChildNodes[0] = new(objects1, Level - 1, extents1, Tree);
                ChildNodes[1] = new(objects2, Level - 1, extents2, Tree);
                ChildNodes[2] = new(objects3, Level - 1, extents3, Tree);
                ChildNodes[3] = new(objects4, Level - 1, extents4, Tree);
            }
        }
        #endregion
    }
}

