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
        public static void LoadEntityObject(EntityObject e, ObjectLayerManager layerManager, Factory1 factory,
            DeviceContext1 deviceContext, ResourceCache resCache)
        {
            if (e is Line line)
            {
                ObjectLayer layer = layerManager.GetLayer(line.Layer.Name);
                DrawingLine drawingLine = new(line, factory, deviceContext, resCache, layer);
                layer.DrawingObjects.Add(drawingLine);
            }
            if (e is Arc arc)
            {
                ObjectLayer layer = layerManager.GetLayer(arc.Layer.Name);
                DrawingArc drawingArc = new(arc, factory, deviceContext, resCache, layer);
                layer.DrawingObjects.Add(drawingArc);
            }
            if (e is Polyline2D polyline2D)
            {
                ObjectLayer layer = layerManager.GetLayer(polyline2D.Layer.Name);
                DrawingPolyline2D drawingPolyline2D = new(polyline2D, factory, deviceContext, resCache, layer);
                layer.DrawingObjects.Add(drawingPolyline2D);
            }
            if (e is Polyline3D polyline3D)
            {
                ObjectLayer layer = layerManager.GetLayer(polyline3D.Layer.Name);
                DrawingPolyline3D drawingPolyline3D = new(polyline3D, factory, deviceContext, resCache, layer);
                layer.DrawingObjects.Add(drawingPolyline3D);
            }
            if (e is Circle circle)
            {
                ObjectLayer layer = layerManager.GetLayer(circle.Layer.Name);
                DrawingCircle drawingCircle = new(circle, factory, deviceContext, resCache, layer);
                layer.DrawingObjects.Add(drawingCircle);
            }
            if (e is Insert block)
            {
                ObjectLayer layer = layerManager.GetLayer(block.Layer.Name);
                DrawingBlock drawingBlock = new(block, factory, deviceContext, resCache, layer);
                layer.DrawingObjects.Add(drawingBlock);
            }
            if (e is netDxf.Entities.Ellipse ellipse)
            {
                ObjectLayer layer = layerManager.GetLayer(ellipse.Layer.Name);
                DrawingEllipse drawingEllipse = new(ellipse, factory, deviceContext, resCache, layer);
                layer.DrawingObjects.Add(drawingEllipse);
            }
            if (e is MText mtext)
            {
                ObjectLayer layer = layerManager.GetLayer(mtext.Layer.Name);
                DrawingMtext drawingMtext = new(mtext, factory, deviceContext, resCache, layer, resCache.FactoryWrite);
                layer.DrawingObjects.Add(drawingMtext);
            }
        }
        public static void LoadDrawingObjects(DxfDocument dxfDocument, ObjectLayerManager layerManager, Factory1 factory,
            DeviceContext1 deviceContext, ResourceCache resCache)
        {
            foreach (var e in dxfDocument.Entities.All)
            {
                LoadEntityObject(e, layerManager, factory, deviceContext, resCache);
            }
        }
        public static DrawingObject GetDrawingObject(EntityObject entity, ObjectLayer layer, Factory1 factory, DeviceContext1 deviceContext, ResourceCache resCache)
        {
            switch (entity)
            {
                case Line line:
                    return new DrawingLine(line, factory, deviceContext, resCache, layer);

                case Arc arc:
                    return new DrawingArc(arc, factory, deviceContext, resCache, layer);

                case Polyline2D polyline2D:
                    return new DrawingPolyline2D(polyline2D, factory, deviceContext, resCache, layer);

                case Polyline3D polyline3D:
                    return new DrawingPolyline3D(polyline3D, factory, deviceContext, resCache, layer);

                case Circle circle:
                    return new DrawingCircle(circle, factory, deviceContext, resCache, layer);

                default:
                    return null;
            }
        }

        public static DrawingSegment GetDrawingSegment(EntityObject entity, ObjectLayer layer, Factory1 factory, DeviceContext1 deviceContext, ResourceCache resCache)
        {
            switch (entity)
            {
                case Line line:
                    return new DrawingLine(line, factory, deviceContext, resCache, layer);

                case Arc arc:
                    return new DrawingArc(arc, factory, deviceContext, resCache, layer);

                default:
                    return null;
            }
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
