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

namespace Direct2DDXFViewer
{
    public class QuadTreeNode
    {
        #region Fields
        private Factory1 _factory;
        private DeviceContext1 _deviceContext;
        #endregion

        #region Properties
        public Bitmap Bitmap { get; set; }
        public List<DrawingObject> DrawingObjects { get; set; } = [];
        public RawMatrix3x2 ExtentsMatrix { get; set; }
        public Rect Bounds { get; set; }
        public Rect DestRect { get; set; }
        public Size2F Size { get; set; }
        public int Level { get; set; }
        public QuadTreeNode[] ChildNodes { get; set; }
        #endregion

        #region Constructors
        public QuadTreeNode(Factory1 factory, DeviceContext1 deviceContext, List<DrawingObject> drawingObjects, RawMatrix3x2 extentsMatrix, Rect bounds, Rect destRect, Size2F size, int level)
        {
            _factory = factory;
            _deviceContext = deviceContext;
            DrawingObjects = drawingObjects;
            ExtentsMatrix = extentsMatrix;
            Bounds = bounds;
            DestRect = destRect;
            Size = size;
            Level = level;

            Subdivide();
        }
        #endregion

        #region Methods
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
        public void DrawBitmap()
        {
            BitmapRenderTarget bitmapRenderTarget = new(_deviceContext, CompatibleRenderTargetOptions.None, new Size2F((float)Bounds.Width, (float)Bounds.Height));
            foreach (var drawingObject in DrawingObjects)
            {
                drawingObject.DrawToRenderTarget(bitmapRenderTarget, 1, drawingObject.Brush, drawingObject.HairlineStrokeStyle);
            }
            Bitmap = bitmapRenderTarget.Bitmap;
            bitmapRenderTarget.Dispose();
        }
        private void Subdivide()
        {
            if (Level > 0)
            {
                ChildNodes = new QuadTreeNode[4];

                // Represents the dxf coordinate bounds of each quadrant.
                Size halfBoundsSize = new(Bounds.Width / 2, Bounds.Height / 2);
                Rect bounds1 = new(Bounds.Left, Bounds.Top, halfBoundsSize.Width, halfBoundsSize.Height);
                Rect bounds2 = new(Bounds.Left + halfBoundsSize.Width, Bounds.Top, halfBoundsSize.Width, halfBoundsSize.Height);
                Rect bounds3 = new(Bounds.Left, Bounds.Top + halfBoundsSize.Height, halfBoundsSize.Width, halfBoundsSize.Height);
                Rect bounds4 = new(Bounds.Left + halfBoundsSize.Width, Bounds.Top + halfBoundsSize.Height, halfBoundsSize.Width, halfBoundsSize.Height);

                // Represents the destination rectangle of each quadrant.
                Size halfDestRectSize = new(DestRect.Width / 2, DestRect.Height / 2);
                Rect destRect1 = new(DestRect.Left, DestRect.Top, halfDestRectSize.Width, halfDestRectSize.Height);
                Rect destRect2 = new(DestRect.Left + halfDestRectSize.Width, DestRect.Top, halfDestRectSize.Width, halfDestRectSize.Height);
                Rect destRect3 = new(DestRect.Left, DestRect.Top + halfDestRectSize.Height, halfDestRectSize.Width, halfDestRectSize.Height);
                Rect destRect4 = new(DestRect.Left + halfDestRectSize.Width, DestRect.Top + halfDestRectSize.Height, halfDestRectSize.Width, halfDestRectSize.Height);

                RawMatrix3x2 m1 = new(ExtentsMatrix.M11, ExtentsMatrix.M12, ExtentsMatrix.M21, ExtentsMatrix.M22,
                            ExtentsMatrix.M31 - (float)(quadrantDestSize.Width * i), ExtentsMatrix.M32 - (float)(quadrantDestSize.Height * j));

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

                ChildNodes[0] = new(_factory, _deviceContext, objects1, bounds1, destRect1, Level - 1);
                ChildNodes[1] = new(_factory, _deviceContext, objects2, bounds2, destRect1, Level - 1);
                ChildNodes[2] = new(_factory, _deviceContext, objects3, bounds3, destRect1, Level - 1);
                ChildNodes[3] = new(_factory, _deviceContext, objects4, bounds4, destRect1, Level - 1);
            }
            else // if Level == 0, this means the node is the final leaf node and thus will be used to draw
            {
                DrawBitmap();
            }
        }
        #endregion
    }
}

