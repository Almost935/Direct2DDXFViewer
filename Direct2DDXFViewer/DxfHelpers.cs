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
using System.Windows.Documents;

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

        public static void DrawLine(Line line, Factory factory, RenderTarget target, Brush brush, float thickness)
        {
            PathGeometry pathGeometry = GetLineGeometry(line, factory);
            target.DrawGeometry(pathGeometry, brush, thickness);
        }
        public static void DrawArc(Arc arc, Factory factory, RenderTarget target, Brush brush, float thickness)
        {
            PathGeometry pathGeometry = GetArcGeometry(arc, factory);
            target.DrawGeometry(pathGeometry, brush, thickness);
        }
        public static void DrawPolyline(Polyline2D pline, Factory factory, RenderTarget target, Brush brush, float thickness)
        {
            PathGeometry pathGeometry = GetPolylineGeometry(pline, factory);
            target.DrawGeometry(pathGeometry, brush, thickness);
        }
        public static void DrawPolyline(Polyline3D pline, Factory factory, RenderTarget target, Brush brush, float thickness)
        {
            PathGeometry pathGeometry = GetPolylineGeometry(pline, factory);
            target.DrawGeometry(pathGeometry, brush, thickness);
        }

        public static PathGeometry GetLineGeometry(Line line, Factory factory)
        {
            PathGeometry pathGeometry = new(factory);
            var sink = pathGeometry.Open();
            sink.BeginFigure(new RawVector2((float)line.StartPoint.X, (float)line.StartPoint.Y), FigureBegin.Filled);
            sink.AddLine(new RawVector2((float)line.EndPoint.X, (float)line.EndPoint.Y));
            sink.EndFigure(FigureEnd.Open);
            sink.Close();
            sink.Dispose();
            
            return pathGeometry;
        }
        public static PathGeometry GetArcGeometry(Arc arc, Factory factory)
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
            var sink = pathGeometry.Open();
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
            sink.Dispose();

            return pathGeometry;
        }
        public static PathGeometry GetPolylineGeometry(Polyline2D pline, Factory factory)
        {
            PathGeometry pathGeometry = new(factory);
            
            var sink = pathGeometry.Open();
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
            sink.Dispose();

            return pathGeometry;
        }
        public static PathGeometry GetPolylineGeometry(Polyline3D pline, Factory factory)
        {
            PathGeometry pathGeometry = new(factory);

            var sink = pathGeometry.Open();
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
            sink.Dispose();

            return pathGeometry;
        }
    }
}
