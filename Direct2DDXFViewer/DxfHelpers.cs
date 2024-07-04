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
        public static void LoadDrawingObjects(DxfDocument dxfDocument, ObjectLayerManager layerManager, Factory factory, 
            RenderTarget target)
        {
            foreach (var e in dxfDocument.Entities.Lines)
            {
                DrawingLine drawingLine = new(e, factory, target);
                drawingLine.UpdateGeometry();

                if (layerManager.Layers.TryGetValue(e.Layer.Name, out ObjectLayer layer))
                {
                    layer.DrawingObjects.Add(drawingLine);
                }
                else
                {
                    ObjectLayer objectLayer = new()
                    {
                        Name = e.Layer.Name
                    };
                    objectLayer.DrawingObjects.Add(drawingLine);
                }
            }

            foreach (var pline2D in dxfDocument.Entities.Polylines2D)
            {
                DrawingPolyline2D drawingPolyline2D = new(pline2D, factory, target);
                drawingPolyline2D.UpdateGeometry();

                if (layerManager.Layers.TryGetValue(pline2D.Layer.Name, out ObjectLayer layer))
                {
                    layer.DrawingObjects.Add(drawingPolyline2D);
                }
                else
                {
                    ObjectLayer objectLayer = new()
                    {
                        Name = pline2D.Layer.Name
                    };
                    objectLayer.DrawingObjects.Add(drawingPolyline2D);
                }
            }

            foreach (var pline3D in dxfDocument.Entities.Polylines3D)
            {
                DrawingPolyline3D drawingPolyline3D = new(pline3D, factory, target);
                drawingPolyline3D.UpdateGeometry();

                if (layerManager.Layers.TryGetValue(pline3D.Layer.Name, out ObjectLayer layer))
                {
                    layer.DrawingObjects.Add(drawingPolyline3D);
                }
                else
                {
                    ObjectLayer objectLayer = new()
                    {
                        Name = pline3D.Layer.Name
                    };
                    objectLayer.DrawingObjects.Add(drawingPolyline3D);
                }
            }
        }

        public static void DrawLine(DrawingLine drawingLine, Factory factory, RenderTarget target, float thickness, Brush brush, StrokeStyle strokeStyle = null)
        {
            target.DrawLine(drawingLine.StartPoint, drawingLine.EndPoint, brush, thickness, strokeStyle);
        }
        public static void DrawArc(Arc arc, Factory factory, RenderTarget target, float thickness)
        {
            Geometry geometry = GetArcGeometry(arc, factory);
            SolidColorBrush brush = new(target, GetEntityColor(arc));
            target.DrawGeometry(geometry, brush, thickness);

            brush.Dispose();
            geometry.Dispose();
        }
        public static void DrawPolyline(DrawingPolyline2D pline, Factory factory, RenderTarget target, float thickness, Brush brush,
            StrokeStyle strokeStyle = null)
        {
            target.DrawGeometry(pline.Geometry, brush, thickness, strokeStyle);
        }
        public static void DrawPolyline(DrawingPolyline3D pline, Factory factory, RenderTarget target, float thickness, Brush brush,
            StrokeStyle strokeStyle = null)
        {
            target.DrawGeometry(pline.Geometry, brush, thickness, strokeStyle);
        }
        public static void DrawCircle(Circle circle, Factory factory, RenderTarget target, float thickness)
        {
            Geometry geometry = GetCircleGeometry(circle, factory);
            SolidColorBrush brush = new(target, GetEntityColor(circle));
            target.DrawGeometry(geometry, brush, thickness);
            
            geometry.Dispose();
        }
        public static void DrawInsert(Insert insert, Factory factory, RenderTarget target, float thickness)
        {
            
        }

        public static Geometry GetLineGeometry(Line line, Factory factory)
        {
            PathGeometry pathGeometry = new(factory);

            using (var sink = pathGeometry.Open())
            {
                sink.BeginFigure(new RawVector2((float)line.StartPoint.X, (float)line.StartPoint.Y), FigureBegin.Filled);
                sink.AddLine(new RawVector2((float)line.EndPoint.X, (float)line.EndPoint.Y));
                sink.EndFigure(FigureEnd.Open);
                sink.Close();
            }

            return pathGeometry;
        }
        public static Geometry GetArcGeometry(Arc arc, Factory factory)
        {
            // Start by getting start and end points using NetDxf ToPolyline2D method
            RawVector2 start = new(
                (float)arc.ToPolyline2D(2).Vertexes.First().Position.X,
                (float)arc.ToPolyline2D(2).Vertexes.First().Position.Y);
            RawVector2 end = new(
                (float)arc.ToPolyline2D(2).Vertexes.Last().Position.X,
                (float)arc.ToPolyline2D(2).Vertexes.Last().Position.Y);

            // Get sweep and find out if large arc 
            double sweep;
            if (arc.EndAngle < arc.StartAngle)
            {
                sweep = (360 + arc.EndAngle) - arc.StartAngle;
            }
            else
            {
                sweep = Math.Abs(arc.EndAngle - arc.StartAngle);
            }
            bool isLargeArc = sweep >= 180;

            PathGeometry pathGeometry = new(factory);

            using (var sink = pathGeometry.Open())
            {
                sink.BeginFigure(start, FigureBegin.Filled);

                ArcSegment arcSegment = new ArcSegment()
                {
                    Point = end,
                    Size = new((float)arc.Radius, (float)arc.Radius),
                    SweepDirection = SharpDX.Direct2D1.SweepDirection.CounterClockwise,
                    RotationAngle = (float)sweep,
                    ArcSize = isLargeArc ? ArcSize.Large : ArcSize.Small
                };
                sink.AddArc(arcSegment);
                sink.EndFigure(FigureEnd.Open);
                sink.Close();
            }

            return pathGeometry;
        }
        public static Geometry GetPolylineGeometry(Polyline2D pline, Factory factory)
        {
            PathGeometry pathGeometry = new(factory);

            using (var sink = pathGeometry.Open())
            {
                RawVector2 start = new((float)pline.Vertexes.First().Position.X, (float)pline.Vertexes.First().Position.Y);
                sink.BeginFigure(start, FigureBegin.Hollow);

                var entities = pline.Explode();
                foreach (var e in entities)
                {
                    if (e is Line line)
                    {
                        RawVector2 end = new((float)line.EndPoint.X, (float)line.EndPoint.Y);
                        sink.AddLine(end);
                    }
                    if (e is Arc arc)
                    {
                        RawVector2 end = new(
                            (float)arc.ToPolyline2D(2).Vertexes.Last().Position.X,
                            (float)arc.ToPolyline2D(2).Vertexes.Last().Position.Y);

                        // Get sweep and find out if large arc 
                        double sweep;
                        if (arc.EndAngle < arc.StartAngle)
                        {
                            sweep = (360 + arc.EndAngle) - arc.StartAngle;
                        }
                        else
                        {
                            sweep = Math.Abs(arc.EndAngle - arc.StartAngle);
                        }
                        bool isLargeArc = sweep >= 180;

                        ArcSegment arcSegment = new ArcSegment()
                        {
                            Point = end,
                            Size = new((float)arc.Radius, (float)arc.Radius),
                            SweepDirection = SharpDX.Direct2D1.SweepDirection.CounterClockwise,
                            RotationAngle = (float)sweep
                        };
                        arcSegment.ArcSize = isLargeArc ? ArcSize.Large : ArcSize.Small;

                        sink.AddArc(arcSegment);
                    }
                }

                sink.EndFigure(pline.IsClosed ? FigureEnd.Closed : FigureEnd.Open);
                sink.Close();
            }

            return pathGeometry;
        }
        public static Geometry GetPolylineGeometry(Polyline3D pline, Factory factory)
        {
            PathGeometry pathGeometry = new(factory);

            using (var sink = pathGeometry.Open())
            {
                RawVector2 start = new((float)pline.Vertexes.First().X, (float)pline.Vertexes.First().Y);
                sink.BeginFigure(start, FigureBegin.Hollow);

                var entities = pline.Explode();
                foreach (var e in entities)
                {
                    if (e is Line line)
                    {
                        RawVector2 end = new((float)line.EndPoint.X, (float)line.EndPoint.Y);
                        sink.AddLine(end);
                    }
                    if (e is Arc arc)
                    {
                        RawVector2 end = new(
                            (float)arc.ToPolyline2D(2).Vertexes.Last().Position.X,
                            (float)arc.ToPolyline2D(2).Vertexes.Last().Position.Y);

                        // Get sweep and find out if large arc 
                        double sweep;
                        if (arc.EndAngle < arc.StartAngle)
                        {
                            sweep = (360 + arc.EndAngle) - arc.StartAngle;
                        }
                        else
                        {
                            sweep = Math.Abs(arc.EndAngle - arc.StartAngle);
                        }
                        bool isLargeArc = sweep >= 180;

                        ArcSegment arcSegment = new ArcSegment()
                        {
                            Point = end,
                            Size = new((float)arc.Radius, (float)arc.Radius),
                            SweepDirection = SharpDX.Direct2D1.SweepDirection.CounterClockwise,
                            RotationAngle = (float)sweep
                        };
                        arcSegment.ArcSize = isLargeArc ? ArcSize.Large : ArcSize.Small;

                        sink.AddArc(arcSegment);
                    }
                }

                sink.EndFigure(pline.IsClosed ? FigureEnd.Closed : FigureEnd.Open);
                sink.Close();
            }

            return pathGeometry;
        }
        public static Geometry GetCircleGeometry(Circle circle, Factory factory)
        {
            Ellipse ellipse = new(new RawVector2((float)circle.Center.X, (float)circle.Center.Y), (float)circle.Radius, (float)circle.Radius);
            EllipseGeometry ellipseGeometry = new(factory, ellipse);
            
            return ellipseGeometry;
        }
        public static GeometryGroup GetInsertGeometry(Insert insert, Factory factory)
        {
            var entities = insert.Explode();
            Geometry[] geometries = new Geometry[entities.Count];

            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i] is Line line)
                {
                    geometries[i] = GetLineGeometry(line, factory);
                }
                if (entities[i] is Arc arc)
                {
                    geometries[i] = GetArcGeometry(arc, factory);
                }
                if (entities[i] is Polyline2D polyline2D)
                {
                    geometries[i] = GetPolylineGeometry(polyline2D, factory);
                }
                if (entities[i] is Polyline3D polyline3D)
                {
                    geometries[i] = GetPolylineGeometry(polyline3D, factory);
                }
                if (entities[i] is Circle circle)
                {
                    geometries[i] = GetCircleGeometry(circle, factory);
                }
            }

            return new GeometryGroup(factory, FillMode.Alternate, geometries);
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
