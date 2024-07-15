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
    public class DrawingPolyline3D : DrawingObject
    {
        #region Fields
        private Polyline3D _dxfPolyline3D;
        #endregion

        #region Properties
        public Polyline3D DxfPolyline3D
        {
            get { return _dxfPolyline3D; }
            set
            {
                _dxfPolyline3D = value;
                OnPropertyChanged(nameof(DxfPolyline3D));
            }
        }
        #endregion

        #region Constructor
        public DrawingPolyline3D(Polyline3D dxfPolyline3D, Factory1 factory, RenderTarget renderTarget)
        {
            DxfPolyline3D = dxfPolyline3D;
            Entity = dxfPolyline3D;
            Factory = factory;

            GetStrokeStyle();
            UpdateBrush();
        }
        #endregion

        #region Methods
        public override void Draw(RenderTarget target, float thickness, Brush brush)
        {
            target.DrawGeometry(Geometry, brush, thickness, StrokeStyle);
        }
        public override void UpdateGeometry()
        {
            PathGeometry pathGeometry = new(Factory);

            using (var sink = pathGeometry.Open())
            {
                RawVector2 start = new((float)DxfPolyline3D.Vertexes.First().X, (float)DxfPolyline3D.Vertexes.First().Y);
                sink.BeginFigure(start, FigureBegin.Hollow);

                var entities = DxfPolyline3D.Explode();
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
                            SweepDirection = SweepDirection.CounterClockwise,
                            RotationAngle = (float)sweep,
                            ArcSize = isLargeArc ? ArcSize.Large : ArcSize.Small
                        };

                        sink.AddArc(arcSegment);
                    }
                }

                sink.EndFigure(DxfPolyline3D.IsClosed ? FigureEnd.Closed : FigureEnd.Open);
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
