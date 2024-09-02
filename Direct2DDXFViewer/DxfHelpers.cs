using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using netDxf;
using netDxf.Entities;
using netDxf.Header;
using SharpDX;
using SharpDX.Direct2D1;
using Direct2DControl;
using Direct2DDXFViewer.DrawingObjects;
using SharpDX.Mathematics.Interop;
using System.Windows;

namespace Direct2DDXFViewer
{
    public static class DxfHelpers
    {
        public static Rect GetExtentsFromHeader(DxfDocument doc)
        {
            if (doc == null) return Rect.Empty;

            if (doc.DrawingVariables.TryGetCustomVariable("$EXTMIN", out HeaderVariable extMinHeaderVariable) &&
                doc.DrawingVariables.TryGetCustomVariable("$EXTMAX", out HeaderVariable extMaxHeaderVariable))
            {
                Vector3 extMin = (Vector3)extMinHeaderVariable.Value;
                Vector3 extMax = (Vector3)extMaxHeaderVariable.Value;

                return new Rect(extMin.X, extMin.Y, extMax.X - extMin.X, extMax.Y - extMin.Y);
            }

            return Rect.Empty;
        }

        public static ObjectLayerManager GetLayers(DxfDocument dxfDocument)
        {
            var layerManager = new ObjectLayerManager();

            foreach (var dxfLayer in dxfDocument.Layers)
            {
                var layer = new ObjectLayer { Name = dxfLayer.Name };
                layerManager.Layers.Add(layer.Name, layer);
            }

            return layerManager;
        }

        public static int LoadEntityObject(EntityObject e, ObjectLayerManager layerManager, Factory1 factory,
            DeviceContext1 deviceContext, ResourceCache resCache)
        {
            ObjectLayer layer = layerManager.GetLayer(e.Layer.Name);
            DrawingObject drawingObject = e switch
            {
                Line line => new DrawingLine(line, factory, deviceContext, resCache, layer),
                Arc arc => new DrawingArc(arc, factory, deviceContext, resCache, layer),
                Polyline2D polyline2D => new DrawingPolyline2D(polyline2D, factory, deviceContext, resCache, layer),
                Polyline3D polyline3D => new DrawingPolyline3D(polyline3D, factory, deviceContext, resCache, layer),
                Circle circle => new DrawingCircle(circle, factory, deviceContext, resCache, layer),
                Insert block => new DrawingBlock(block, factory, deviceContext, resCache, layer),
                netDxf.Entities.Ellipse ellipse => new DrawingEllipse(ellipse, factory, deviceContext, resCache, layer),
                MText mtext => new DrawingMtext(mtext, factory, deviceContext, resCache, layer, resCache.FactoryWrite),
                _ => null
            };

            if (drawingObject != null)
            {
                layer.DrawingObjects.Add(drawingObject);
                return drawingObject.EntityCount;
            }

            return 0;
        }

        public static int LoadDrawingObjects(DxfDocument dxfDocument, ObjectLayerManager layerManager, Factory1 factory,
            DeviceContext1 deviceContext, ResourceCache resCache)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            int count = dxfDocument.Entities.All.Sum(e => LoadEntityObject(e, layerManager, factory, deviceContext, resCache));

            stopwatch.Stop();
            Debug.WriteLine($"LoadDrawingObjects: {stopwatch.ElapsedMilliseconds} ms");

            return count;
        }

        public static DrawingObject GetDrawingObject(EntityObject entity, ObjectLayer layer, Factory1 factory, DeviceContext1 deviceContext, ResourceCache resCache)
        {
            return entity switch
            {
                Line line => new DrawingLine(line, factory, deviceContext, resCache, layer),
                Arc arc => new DrawingArc(arc, factory, deviceContext, resCache, layer),
                Polyline2D polyline2D => new DrawingPolyline2D(polyline2D, factory, deviceContext, resCache, layer),
                Polyline3D polyline3D => new DrawingPolyline3D(polyline3D, factory, deviceContext, resCache, layer),
                Circle circle => new DrawingCircle(circle, factory, deviceContext, resCache, layer),
                _ => null
            };
        }

        public static DrawingSegment GetDrawingSegment(EntityObject entity, ObjectLayer layer, Factory1 factory, DeviceContext1 deviceContext, ResourceCache resCache)
        {
            return entity switch
            {
                Line line => new DrawingLine(line, factory, deviceContext, resCache, layer),
                Arc arc => new DrawingArc(arc, factory, deviceContext, resCache, layer),
                _ => null
            };
        }

        public static RawColor4 GetEntityColor(EntityObject entity)
        {
            var color = entity.Color.IsByLayer ? entity.Layer.Color : entity.Color;
            return color.R == 255 && color.G == 255 && color.B == 255
                ? new RawColor4(0.0f, 0.0f, 0.0f, 1.0f)
                : new RawColor4(color.R / 255f, color.G / 255f, color.B / 255f, 1.0f);
        }
    }
}