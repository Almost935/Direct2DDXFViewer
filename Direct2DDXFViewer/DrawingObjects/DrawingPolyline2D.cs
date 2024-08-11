using netDxf.Entities;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class DrawingPolyline2D : DrawingObject
    {
        #region Fields
        private Polyline2D _dxfPolyline2D;
        #endregion

        #region Properties
        public Polyline2D DxfPolyline2D
        {
            get { return _dxfPolyline2D; }
            set
            {
                _dxfPolyline2D = value;
                OnPropertyChanged(nameof(DxfPolyline2D));
            }
        }

        public ObservableCollection<DrawingSegment> DrawingSegments { get; set; } = new(); 
        #endregion

        #region Constructor
        public DrawingPolyline2D(Polyline2D dxfPolyline2D, Factory1 factory, DeviceContext1 deviceContext)
        {
            DxfPolyline2D = dxfPolyline2D;
            Entity = dxfPolyline2D;
            Factory = factory;
            DeviceContext = deviceContext;

            GetStrokeStyle();
            UpdateBrush();
        }
        #endregion

        #region Methods
        public override void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness, Brush brush)
        {
            foreach (var segment in DrawingSegments)
            {
                if (segment is DrawingLine line)
                {
                    line.DrawToDeviceContext(deviceContext, thickness, brush);
                }

            }
        }
        public override void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush)
        {
            foreach (var segment in DrawingSegments)
            {
                if (segment is DrawingLine line)
                {
                    line.DrawToRenderTarget(target, thickness, brush);
                }

            }
        }
        public override bool DrawingObjectIsInRect(Rect rect)
        {
            return true;
            //return Bounds.IntersectsWith(rect) || Bounds.Contains(rect);
        }
        public override void UpdateGeometry()
        {
            foreach (var e in DxfPolyline2D.Explode())
            {
                if (e is Line line)
                {
                    DrawingSegments.Add(new DrawingLine(line, Factory, DeviceContext));
                }
                if (e is Arc arc)
                {
                    DrawingSegments.Add(new DrawingArc(arc, Factory, DeviceContext));
                }
            }

            //PathGeometry pathGeometry = new(Factory);

            //using (var sink = pathGeometry.Open())
            //{
            //    RawVector2 start = new((float)DxfPolyline2D.Vertexes.First().Position.X, (float)DxfPolyline2D.Vertexes.First().Position.Y);
            //    sink.BeginFigure(start, FigureBegin.Hollow);

            //    var entities = DxfPolyline2D.Explode();
            //    foreach (var e in entities)
            //    {
            //        if (e is Line line)
            //        {
            //            RawVector2 end = new((float)line.EndPoint.X, (float)line.EndPoint.Y);
            //            sink.AddLine(end);
            //        }
            //        if (e is Arc arc)
            //        {
            //            RawVector2 end = new(
            //                (float)arc.ToPolyline2D(2).Vertexes.Last().Position.X,
            //                (float)arc.ToPolyline2D(2).Vertexes.Last().Position.Y);

            //            // Get sweep and find out if large arc 
            //            double sweep;
            //            if (arc.EndAngle < arc.StartAngle)
            //            {
            //                sweep = (360 + arc.EndAngle) - arc.StartAngle;
            //            }
            //            else
            //            {
            //                sweep = Math.Abs(arc.EndAngle - arc.StartAngle);
            //            }
            //            bool isLargeArc = sweep >= 180;

            //            ArcSegment arcSegment = new()
            //            {
            //                Point = end,
            //                Size = new((float)arc.Radius, (float)arc.Radius),
            //                SweepDirection = SweepDirection.CounterClockwise,
            //                RotationAngle = (float)sweep
            //            }; 
            //            arcSegment.ArcSize = isLargeArc ? ArcSize.Large : ArcSize.Small;

            //            sink.AddArc(arcSegment);
            //        }
            //    }
            //    sink.EndFigure(DxfPolyline2D.IsClosed ? FigureEnd.Closed : FigureEnd.Open);
            //    sink.Close();

            //    Geometry = pathGeometry;

            //    var bounds = Geometry.GetBounds();
            //    Bounds = new(bounds.Left, bounds.Top, Math.Abs(bounds.Right - bounds.Left), Math.Abs(bounds.Bottom - bounds.Top));
            //    Debug.WriteLine($"Bounds: {Bounds}");
                
            //    // Simplify the geometry
            //    var simplifiedGeometry = new PathGeometry(Factory);
            //    using (var simplifiedSink = simplifiedGeometry.Open())
            //    {
            //        pathGeometry.Simplify(GeometrySimplificationOption.CubicsAndLines, simplifiedSink);
            //        simplifiedSink.Close();
            //    }

            //    Debug.WriteLine($"simplifiedGeometry.GetBounds(: {simplifiedGeometry.GetBounds()}");
            //}
        }
        #endregion
    }
}
