using Direct2DControl;
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
using System.Windows;

using Ellipse = SharpDX.Direct2D1.Ellipse;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class DrawingCircle : DrawingSegment
    {
        #region Fields
        private Circle _dxfCircle;
        #endregion

        #region Properties
        public Circle DxfCircle
        {
            get { return _dxfCircle; }
            set
            {
                _dxfCircle = value;
                OnPropertyChanged(nameof(DxfCircle));
            }
        }
        #endregion

        #region Constructor
        public DrawingCircle(Circle dxfCircle, Factory1 factory, DeviceContext1 deviceContext, ResourceCache resCache, ObjectLayer layer)
        {
            DxfCircle = dxfCircle;
            Entity = dxfCircle;
            Factory = factory;
            DeviceContext = deviceContext;
            ResCache = resCache;
            Layer = layer;
            EntityCount = 1;

            GetStrokeStyle();
            UpdateBrush();
        }
        #endregion

        #region Methods
        public override void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness, Brush brush)
        {
            deviceContext.DrawGeometry(Geometry, brush, thickness);
        }
        public override void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness, Brush brush, StrokeStyle1 strokeStyle)
        {
            deviceContext.DrawGeometry(Geometry, brush, thickness, strokeStyle);
        }
        public override void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush)
        {
            target.DrawGeometry(Geometry, brush, thickness);
        }
        public override void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush, StrokeStyle1 strokeStyle)
        {
            target.DrawGeometry(Geometry, brush, thickness, strokeStyle);
        }
        public override bool DrawingObjectIsInRect(Rect rect)
        {
            return Bounds.IntersectsWith(rect) || Bounds.Contains(rect);
        }

        
        public override void InitializeGeometries()
        {
            Ellipse ellipse = new(new RawVector2((float)DxfCircle.Center.X, (float)DxfCircle.Center.Y), (float)DxfCircle.Radius, (float)DxfCircle.Radius);
            EllipseGeometry ellipseGeometry = new(Factory, ellipse);

            Geometry = ellipseGeometry;

            var bounds = Geometry.GetWidenedBounds(_hitTestStrokeThickness);
            Bounds = new(bounds.Left, bounds.Top, Math.Abs(bounds.Right - bounds.Left), Math.Abs(bounds.Bottom - bounds.Top));
        }
        public override async Task UpdateGeometriesAsync()
        {
            await Task.Run(() => InitializeGeometries());
        }
        public override List<GeometryRealization> GetGeometryRealization(float thickness)
        {
            List<GeometryRealization> geometryRealizations = [];

            if (Geometry is not null)
            {
                geometryRealizations.Add(new(DeviceContext, Geometry, 0.5f, thickness, HairlineStrokeStyle));
            }

            return geometryRealizations;
        }
        public override bool Hittest(RawVector2 p, float thickness)
        {
            return Geometry.StrokeContainsPoint(p, thickness);
        }
        #endregion
    }
}
