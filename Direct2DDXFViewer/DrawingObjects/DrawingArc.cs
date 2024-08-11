using netDxf.Entities;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class DrawingArc : DrawingSegment
    {
        #region Fields
        private Arc _dxfArc;
        #endregion

        #region Properties
        public Arc DxfArc
        {
            get { return _dxfArc; }
            set
            {
                _dxfArc = value;
                OnPropertyChanged(nameof(DxfArc));
            }
        }
        #endregion

        #region Constructor
        public DrawingArc(Arc dxfArc, Factory1 factory, DeviceContext1 deviceContext)
        {
            DxfArc = dxfArc;
            Entity = dxfArc;
            Factory = factory;
            DeviceContext = deviceContext;

            GetStrokeStyle();
            UpdateBrush();
        }
        #endregion

        #region Methods
        public override void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness, Brush brush)
        {
            deviceContext.DrawGeometry(Geometry, brush, thickness, StrokeStyle);
        }
        public override void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush)
        {
            target.DrawGeometry(Geometry, brush, thickness, StrokeStyle);
        }
        public override bool DrawingObjectIsInRect(Rect rect)
        {
            return Bounds.IntersectsWith(rect) || Bounds.Contains(rect);
        }
        public override void UpdateGeometry()
        {
            // Start by getting start and end points using NetDxf ToPolyline2D method
            StartPoint = new(
                (float)DxfArc.ToPolyline2D(2).Vertexes.First().Position.X,
                (float)DxfArc.ToPolyline2D(2).Vertexes.First().Position.Y);
            EndPoint = new(
                (float)DxfArc.ToPolyline2D(2).Vertexes.Last().Position.X,
                (float)DxfArc.ToPolyline2D(2).Vertexes.Last().Position.Y);

            // Get sweep and find out if large arc 
            double sweep;
            if (DxfArc.EndAngle < DxfArc.StartAngle)
            {
                sweep = (360 + DxfArc.EndAngle) - DxfArc.StartAngle;
            }
            else
            {
                sweep = Math.Abs(DxfArc.EndAngle - DxfArc.StartAngle);
            }
            bool isLargeArc = sweep >= 180;

            PathGeometry pathGeometry = new(Factory);

            using (var sink = pathGeometry.Open())
            {
                sink.BeginFigure(StartPoint, FigureBegin.Filled);

                ArcSegment arcSegment = new()
                {
                    Point = EndPoint,
                    Size = new((float)DxfArc.Radius, (float)DxfArc.Radius),
                    SweepDirection = SweepDirection.CounterClockwise,
                    RotationAngle = (float)sweep,
                    ArcSize = isLargeArc ? ArcSize.Large : ArcSize.Small
                };
                sink.AddArc(arcSegment);
                sink.EndFigure(FigureEnd.Open);
                sink.Close();

                Geometry = pathGeometry;

                var bounds = Geometry.GetBounds();
                Bounds = new(bounds.Left, bounds.Top, Math.Abs(bounds.Right - bounds.Left), Math.Abs(bounds.Bottom - bounds.Top));
            }
        }
        #endregion
    }
}
