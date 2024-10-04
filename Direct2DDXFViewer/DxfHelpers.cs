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
using netDxf.Tables;
using System.Net;
using System.Windows.Media;
using PathGeometry = SharpDX.Direct2D1.PathGeometry;
using ArcSegment = SharpDX.Direct2D1.ArcSegment;
using SweepDirection = SharpDX.Direct2D1.SweepDirection;
using Geometry = SharpDX.Direct2D1.Geometry;
using Ellipse = SharpDX.Direct2D1.Ellipse;
using EllipseGeometry = SharpDX.Direct2D1.EllipseGeometry;

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

            stopwatch.Restart();

            //Parallel.ForEach(layerManager.Layers.Values, layer =>
            //{
            //    foreach (var obj in layer.DrawingObjects)
            //    {
            //        obj.UpdateGeometry();
            //    }

            //    //    Parallel.ForEach(layer.DrawingObjects, obj =>
            //    //{
            //    //    obj.UpdateGeometry();
            //    //});
            //});

            foreach (var layer in layerManager.Layers.Values)
            {
                foreach (var obj in layer.DrawingObjects)
                {
                    obj.UpdateGeometry();
                }
            }

            stopwatch.Stop();
            Debug.WriteLine($"Load Geometries: {stopwatch.ElapsedMilliseconds} ms");

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


        //public static Geometry GetEntityGeometry(Factory1 factory, DeviceContext1 deviceContext, EntityObject e)
        //{
        //    return e switch
        //    {
        //        Line line => GetLineGeometry(factory, deviceContext, line),
        //        Arc arc => GetArcGeometry(factory, deviceContext, arc),
        //        Circle circle => GetCircleGeometry(factory, deviceContext, circle),
        //        netDxf.Entities.Ellipse ellipse => GetEllipseGeometry(factory, deviceContext, ellipse),
        //        _ => null
        //    };
        //}
        //public static Geometry GetSegmentGeometry(Factory1 factory, DeviceContext1 deviceContext, EntityObject e)
        //{
        //    return e switch
        //    {
        //        Line line => GetLineGeometry(factory, deviceContext, line),
        //        Arc arc => GetArcGeometry(factory, deviceContext, arc),
        //        Circle circle => GetCircleGeometry(factory, deviceContext, circle),
        //        netDxf.Entities.Ellipse ellipse => GetEllipseGeometry(factory, deviceContext, ellipse),
        //        _ => null
        //    };
        //}
        //public static Geometry GetLineGeometry(Factory1 factory, DeviceContext1 deviceContext, Line line)
        //{
        //    PathGeometry pathGeometry = new(factory);
        //    using (var sink = pathGeometry.Open())
        //    {
        //        sink.BeginFigure(new RawVector2((float)line.StartPoint.X, (float)line.StartPoint.Y), FigureBegin.Filled);
        //        sink.AddLine(new RawVector2((float)line.EndPoint.X, (float)line.EndPoint.Y));
        //        sink.EndFigure(FigureEnd.Open);
        //        sink.Close();
        //    }
        //    return pathGeometry;
        //}
        //public static Geometry GetArcGeometry(Factory1 factory, DeviceContext1 deviceContext, Arc arc)
        //{
        //    // Start by getting start and end points using NetDxf ToPolyline2D method
        //    RawVector2 startPoint = new(
        //        (float)arc.ToPolyline2D(2).Vertexes.First().Position.X,
        //        (float)arc.ToPolyline2D(2).Vertexes.First().Position.Y);
        //    RawVector2 endPoint = new(
        //        (float)arc.ToPolyline2D(2).Vertexes.Last().Position.X,
        //        (float)arc.ToPolyline2D(2).Vertexes.Last().Position.Y);

        //    // Get sweep and find out if large arc 
        //    double sweep;
        //    if (arc.EndAngle < arc.StartAngle)
        //    {
        //        sweep = (360 + arc.EndAngle) - arc.StartAngle;
        //    }
        //    else
        //    {
        //        sweep = Math.Abs(arc.EndAngle - arc.StartAngle);
        //    }
        //    bool isLargeArc = sweep >= 180;

        //    PathGeometry pathGeometry = new(factory);
        //    using (var sink = pathGeometry.Open())
        //    {
        //        sink.BeginFigure(startPoint, FigureBegin.Filled);

        //        ArcSegment arcSegment = new()
        //        {
        //            Point = endPoint,
        //            Size = new((float)arc.Radius, (float)arc.Radius),
        //            SweepDirection = SweepDirection.Clockwise,
        //            RotationAngle = (float)sweep,
        //            ArcSize = isLargeArc ? ArcSize.Large : ArcSize.Small
        //        };

        //        sink.AddArc(arcSegment);
        //        sink.EndFigure(FigureEnd.Open);
        //        sink.Close();

        //        var simplifiedGeometry = new PathGeometry(factory);

        //        // Open a GeometrySink to store the simplified version of the original geometry
        //        using (var geometrySink = simplifiedGeometry.Open())
        //        {
        //            // Simplify the geometry, reducing it to line segments
        //            pathGeometry.Simplify(GeometrySimplificationOption.CubicsAndLines, 0.25f, geometrySink);
        //            geometrySink.Close();
        //        }
        //        return simplifiedGeometry;
        //    }
        //}
        //public static Geometry GetCircleGeometry(Factory1 factory, DeviceContext1 deviceContext, Circle circle)
        //{
        //    SharpDX.Direct2D1.Ellipse ellipse = new(new RawVector2((float)circle.Center.X, (float)circle.Center.Y), (float)circle.Radius, (float)circle.Radius);
        //    EllipseGeometry ellipseGeometry = new(factory, ellipse);

        //    return ellipseGeometry;
        //}
        //public static Geometry GetEllipseGeometry(Factory1 factory, DeviceContext1 deviceContext, netDxf.Entities.Ellipse ellipse)
        //{
        //    if (ellipse.IsFullEllipse)
        //    {
        //        // Extract properties from the netDxf Ellipse
        //        var center = ellipse.Center;
        //        double majorAxis = ellipse.MajorAxis;
        //        double minorAxis = ellipse.MinorAxis;
        //        double rotation = ellipse.Rotation; // Rotation in degrees

        //        // Convert center coordinates and axes to SharpDX format
        //        var centerPoint = new RawVector2((float)center.X, (float)center.Y);
        //        var width = (float)(majorAxis);
        //        var height = (float)(minorAxis);

        //        Matrix matrix = new();
        //        matrix.RotateAt((float)rotation, centerPoint.X, centerPoint.Y);

        //        // Create the SharpDX Ellipse
        //        Ellipse sharpDxEllipse = new(centerPoint, width / 2, height / 2);

        //        // Create EllipseGeometry
        //        var ellipseGeometry = new EllipseGeometry(factory, sharpDxEllipse);

        //        // Apply rotation if needed
        //        if (rotation != 0)
        //        {
        //            // Apply rotation transformation if required
        //            RawMatrix3x2 transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22, (float)matrix.OffsetX, (float)matrix.OffsetY);
        //            return new TransformedGeometry(factory, ellipseGeometry, transform);
        //        }
        //        else
        //        {
        //            return ellipseGeometry;
        //        }
        //    }
        //    else
        //    {
        //        // Start by getting start and end points using NetDxf ToPolyline2D method
        //        RawVector2 startPoint = new(
        //            (float)ellipse.ToPolyline2D(2).Vertexes.First().Position.X,
        //            (float)ellipse.ToPolyline2D(2).Vertexes.First().Position.Y);
        //        RawVector2 endPoint = new(
        //            (float)ellipse.ToPolyline2D(2).Vertexes.Last().Position.X,
        //            (float)ellipse.ToPolyline2D(2).Vertexes.Last().Position.Y);
        //        var radiusX = (float)(ellipse.MajorAxis / 2);
        //        var radiusY = (float)(ellipse.MinorAxis / 2);
        //        float rotation = (float)ellipse.Rotation; // Rotation in degrees

        //        // Get sweep and find out if large arc 
        //        double sweep;
        //        if (ellipse.EndAngle < ellipse.StartAngle)
        //        {
        //            sweep = (360 + ellipse.EndAngle) - ellipse.StartAngle;
        //        }
        //        else
        //        {
        //            sweep = Math.Abs(ellipse.EndAngle - ellipse.StartAngle);
        //        }
        //        bool isLargeArc = sweep >= 180;

        //        PathGeometry pathGeometry = new(factory);
        //        using (var sink = pathGeometry.Open())
        //        {
        //            sink.BeginFigure(startPoint, FigureBegin.Filled);

        //            ArcSegment arcSegment = new()
        //            {
        //                Point = endPoint,
        //                Size = new(radiusX, radiusY),
        //                SweepDirection = SweepDirection.Clockwise,
        //                RotationAngle = rotation,
        //                ArcSize = isLargeArc ? ArcSize.Large : ArcSize.Small
        //            };

        //            sink.AddArc(arcSegment);
        //            sink.EndFigure(FigureEnd.Open);
        //            sink.Close();

        //            // Apply rotation if needed
        //            if (rotation != 0)
        //            {
        //                Matrix matrix = new();
        //                //matrix.RotateAt((float)rotation, centerPoint.X, centerPoint.Y);

        //                // Apply rotation transformation if required
        //                RawMatrix3x2 transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22, (float)matrix.OffsetX, (float)matrix.OffsetY);

        //                return new TransformedGeometry(factory, pathGeometry, transform);
        //            }
        //            else
        //            {
        //                return pathGeometry;
        //            }
        //        }
        //    }
        //}
    }
}