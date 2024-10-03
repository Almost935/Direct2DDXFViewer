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

        public static (byte r, byte g, byte b, byte a) GetRGBAColor(EntityObject entity)
        {
            byte r, g, b, a;
            if (entity.Color.IsByLayer)
            {
                if (entity.Layer.Color.R == 255 && entity.Layer.Color.G == 255 && entity.Layer.Color.B == 255)
                {
                    r = g = b = 0; a = 255;
                }
                else
                {
                    r = entity.Layer.Color.R; g = entity.Layer.Color.G; b = entity.Layer.Color.B; a = 255;
                }
            }
            else
            {
                if (entity.Color.R == 255 && entity.Color.G == 255 && entity.Color.B == 255)
                {
                    r = g = b = 0; a = 255;
                }
                else
                {
                    r = entity.Color.R; g = entity.Color.G; b = entity.Color.B; a = 255;
                }
            }

            return (r, g, b, a);
        }
    }
}