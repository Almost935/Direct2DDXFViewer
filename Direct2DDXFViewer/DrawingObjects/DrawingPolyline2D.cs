using netDxf.Entities;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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

        RenderTarget Target { get; set; }
        #endregion

        #region Constructor
        public DrawingPolyline2D(Polyline2D dxfPolyline2D, Factory1 factory, RenderTarget renderTarget)
        {
            DxfPolyline2D = dxfPolyline2D;
            Entity = dxfPolyline2D;
            Factory = factory; 

            UpdateBrush();
        }
        #endregion

        #region Methods
        public override void UpdateGeometry()
        {
            PathGeometry pathGeometry = new(Factory);

            using (var sink = pathGeometry.Open())
            {
                RawVector2 start = new((float)DxfPolyline2D.Vertexes.First().Position.X, (float)DxfPolyline2D.Vertexes.First().Position.Y);
                sink.BeginFigure(start, FigureBegin.Hollow);

                var entities = DxfPolyline2D.Explode();
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

                        ArcSegment arcSegment = new()
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

                sink.EndFigure(DxfPolyline2D.IsClosed ? FigureEnd.Closed : FigureEnd.Open);
                sink.Close();

                // Simplify the geometry
                var simplifiedGeometry = new PathGeometry(Factory);
                using (var simplifiedSink = simplifiedGeometry.Open())
                {
                    pathGeometry.Simplify(GeometrySimplificationOption.CubicsAndLines, simplifiedSink);
                    simplifiedSink.Close();
                }
                Geometry = simplifiedGeometry;
            }
        }
        #endregion
    }
}
