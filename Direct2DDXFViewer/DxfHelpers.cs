using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using netDxf;
using netDxf.Header;
using netDxf.Objects;
using netDxf.Entities;
using netDxf.Tables;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;

using Point = System.Windows.Point;
using Geometry = SharpDX.Direct2D1.Geometry;
using PathGeometry = SharpDX.Direct2D1.PathGeometry;
using Brush = SharpDX.Direct2D1.Brush;
using ArcSegment = SharpDX.Direct2D1.ArcSegment;
using SolidColorBrush = SharpDX.Direct2D1.SolidColorBrush;
using System.Windows.Documents;
using SweepDirection = SharpDX.Direct2D1.SweepDirection;
using System.Windows.Media.Media3D;
using EllipseGeometry = SharpDX.Direct2D1.EllipseGeometry;
using Ellipse = SharpDX.Direct2D1.Ellipse;
using Layer = SharpDX.Direct2D1.Layer;
using GeometryGroup = SharpDX.Direct2D1.GeometryGroup;
using Direct2DDXFViewer.DrawingObjects;
using netDxf.Units;
using System.Diagnostics;
using Direct2DControl;

namespace Direct2DDXFViewer
{
    public static class DxfHelpers
    {
        public static Rect GetExtentsFromHeader(DxfDocument doc)
        {
            if (doc is not null)
            {
                if (doc.DrawingVariables.TryGetCustomVariable("$EXTMIN", out HeaderVariable extMinHeaderVariable) &&
                    doc.DrawingVariables.TryGetCustomVariable("$EXTMAX", out HeaderVariable extMaxHeaderVariable))
                {
                    Vector3 extMin = (Vector3)extMinHeaderVariable.Value;
                    Vector3 extMax = (Vector3)extMaxHeaderVariable.Value;

                    return new Rect(extMin.X, extMin.Y, extMax.X - extMin.X, extMax.Y - extMin.Y);
                }
                return Rect.Empty;
            }

            else
            {
                return Rect.Empty;
            }
        }
        public static ObjectLayerManager GetLayers(DxfDocument dxfDocument)
        {
            ObjectLayerManager layerManager = new();

            foreach (var dxfLayer in dxfDocument.Layers)
            {
                ObjectLayer layer = new()
                {
                    Name = dxfLayer.Name,
                };

                layerManager.Layers.Add(layer.Name, layer);
            }

            return layerManager;
        }
        public static void LoadDrawingObjects(DxfDocument dxfDocument, ObjectLayerManager layerManager, Factory1 factory,
            DeviceContext1 deviceContext, ResourceCache resCache)
        {
            Debug.WriteLine($"\ndxfDocument.Entities.Lines.Count(): {dxfDocument.Entities.Lines.Count()}");
            foreach (var line in dxfDocument.Entities.Lines)
            {
                DrawingLine drawingLine = new(line, factory, deviceContext, resCache);
                drawingLine.UpdateGeometry();

                if (layerManager.Layers.TryGetValue(line.Layer.Name, out ObjectLayer layer))
                {
                    layer.DrawingObjects.Add(drawingLine);
                }
                else
                {
                    ObjectLayer objectLayer = new()
                    {
                        Name = line.Layer.Name
                    };
                    objectLayer.DrawingObjects.Add(drawingLine);
                }
            }
            foreach (var arc in dxfDocument.Entities.Arcs)
            {
                DrawingArc drawingArc = new(arc, factory, deviceContext, resCache);
                drawingArc.UpdateGeometry();

                if (layerManager.Layers.TryGetValue(arc.Layer.Name, out ObjectLayer layer))
                {
                    layer.DrawingObjects.Add(drawingArc);
                }
                else
                {
                    ObjectLayer objectLayer = new()
                    {
                        Name = arc.Layer.Name
                    };
                    objectLayer.DrawingObjects.Add(drawingArc);
                }
            }
            foreach (var polyline2D in dxfDocument.Entities.Polylines2D)
            {
                DrawingPolyline2D drawingPolyline2D = new(polyline2D, factory, deviceContext, resCache);
                drawingPolyline2D.UpdateGeometry();

                if (layerManager.Layers.TryGetValue(polyline2D.Layer.Name, out ObjectLayer layer))
                {
                    layer.DrawingObjects.Add(drawingPolyline2D);
                }
                else
                {
                    ObjectLayer objectLayer = new()
                    {
                        Name = polyline2D.Layer.Name
                    };
                    objectLayer.DrawingObjects.Add(drawingPolyline2D);
                }
            }
            foreach (var polyline3D in dxfDocument.Entities.Polylines3D)
            {
                DrawingPolyline3D drawingPolyline3D = new(polyline3D, factory, deviceContext, resCache);
                drawingPolyline3D.UpdateGeometry();

                if (layerManager.Layers.TryGetValue(polyline3D.Layer.Name, out ObjectLayer layer))
                {
                    layer.DrawingObjects.Add(drawingPolyline3D);
                }
                else
                {
                    ObjectLayer objectLayer = new()
                    {
                        Name = polyline3D.Layer.Name
                    };
                    objectLayer.DrawingObjects.Add(drawingPolyline3D);
                }
            }
            foreach (var circle in dxfDocument.Entities.Circles)
            {
                DrawingCircle drawingCircle = new(circle, factory, deviceContext, resCache);
                drawingCircle.UpdateGeometry();

                if (layerManager.Layers.TryGetValue(circle.Layer.Name, out ObjectLayer layer))
                {
                    layer.DrawingObjects.Add(drawingCircle);
                }
                else
                {
                    ObjectLayer objectLayer = new()
                    {
                        Name = circle.Layer.Name
                    };
                    objectLayer.DrawingObjects.Add(drawingCircle);
                }
            }

            Debug.WriteLine($"\nDone Loading Drawing Objects.\n");
        }


        public static RawColor4 GetEntityColor(EntityObject entity)
        {
            if (entity.Color.IsByLayer)
            {
                if (entity.Layer.Color.R == 255 && entity.Layer.Color.G == 255 && entity.Layer.Color.B == 255)
                {
                    return new RawColor4(0.0f, 0.0f, 0.0f, 1.0f);
                }
                else
                {
                    return new RawColor4((float)(entity.Layer.Color.R / 255), (float)(entity.Layer.Color.G / 255), (float)(entity.Layer.Color.B / 255), 1.0f);
                }
            }
            else
            {
                if (entity.Color.R == 255 && entity.Color.G == 255 && entity.Color.B == 255)
                {
                    return new RawColor4(0.0f, 0.0f, 0.0f, 1.0f);
                }
                else
                {
                    return new RawColor4((float)(entity.Color.R) / 255, (float)(entity.Color.G) / 255, (float)(entity.Color.B) / 255, 1.0f);
                }
            }
        }
    }
}
