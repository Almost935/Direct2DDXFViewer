using Direct2DControl;
using Direct2DDXFViewer.Helpers;
using netDxf;
using netDxf.Entities;
using netDxf.Units;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Point = System.Windows.Point;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class DrawingLine : DrawingSegment
    {
        #region Fields
        private Line _dxfLine;
        #endregion

        #region Properties
        public Line DxfLine
        {
            get { return _dxfLine; }
            set
            {
                _dxfLine = value;
                OnPropertyChanged(nameof(DxfLine));
            }
        }
        #endregion

        #region Constructor
        public DrawingLine(Line dxfLine, Factory1 factory, DeviceContext1 deviceContext, ResourceCache resCache, ObjectLayer layer)
        {
            DxfLine = dxfLine;
            Entity = dxfLine;
            Factory = factory;
            DeviceContext = deviceContext;
            ResCache = resCache;
            Layer = layer;
            EntityCount = 1;

            StartPoint = new((float)dxfLine.StartPoint.X, (float)dxfLine.StartPoint.Y);
            EndPoint = new((float)dxfLine.EndPoint.X, (float)dxfLine.EndPoint.Y);

            UpdateGeometry();
            GetStrokeStyle();
            GetThickness();
            UpdateBrush();
        }
        #endregion

        #region Methods
        public override void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness, Brush brush)
        {
            deviceContext.DrawLine(StartPoint, EndPoint, brush, thickness);
        }
        public override void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness, Brush brush, StrokeStyle1 strokeStyle)
        {
            deviceContext.DrawLine(StartPoint, EndPoint, brush, thickness, strokeStyle);
        }
        public override void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush)
        {
            Debug.WriteLine($"DrawingLine thickness: {thickness}");
            target.DrawLine(StartPoint, EndPoint, brush, thickness);
        }
        public override void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush, StrokeStyle1 strokeStyle)
        {
            target.DrawLine(StartPoint, EndPoint, brush, thickness, strokeStyle);
        }
        public override bool DrawingObjectIsInRect(Rect rect)
        {
            return Bounds.IntersectsWith(rect) || Bounds.Contains(rect);
        }
        public override void UpdateGeometry()
        {
            PathGeometry pathGeometry = new(Factory);
            using (var sink = pathGeometry.Open())
            {
                sink.BeginFigure(new RawVector2((float)DxfLine.StartPoint.X, (float)DxfLine.StartPoint.Y), FigureBegin.Filled);
                sink.AddLine(new RawVector2((float)DxfLine.EndPoint.X, (float)DxfLine.EndPoint.Y));
                sink.EndFigure(FigureEnd.Open);
                sink.Close();

                Geometry = pathGeometry;
                var bounds = Geometry.GetBounds();
                Bounds = new(bounds.Left, bounds.Top, Math.Abs(bounds.Right - bounds.Left), Math.Abs(bounds.Bottom - bounds.Top));
            }
        }
        public override bool Hittest(RawVector2 p, float thickness)
        {
            return Geometry.StrokeContainsPoint(p, thickness);
        }
        #endregion
    }
}
