using Direct2DControl;
using netDxf;
using netDxf.Entities;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Diagnostics;
using System.Net;

using Ellipse = SharpDX.Direct2D1.Ellipse;
using EllipseGeometry = SharpDX.Direct2D1.EllipseGeometry;
using Geometry = SharpDX.Direct2D1.Geometry;
using Brush = SharpDX.Direct2D1.Brush;
using PathGeometry = SharpDX.Direct2D1.PathGeometry;
using ArcSegment = SharpDX.Direct2D1.ArcSegment;
using SweepDirection = SharpDX.Direct2D1.SweepDirection;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using SharpDX;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class DrawingEllipse : DrawingObject
    {
        #region Fields
        private netDxf.Entities.Ellipse _dxfEllipse;
        #endregion

        #region Properties
        public netDxf.Entities.Ellipse DxfEllipse
        {
            get { return _dxfEllipse; }
            set
            {
                _dxfEllipse = value;
                OnPropertyChanged(nameof(_dxfEllipse));
            }
        }
        #endregion

        #region Constructor
        public DrawingEllipse(netDxf.Entities.Ellipse dxfEllipse, Factory1 factory, DeviceContext1 deviceContext, ResourceCache resCache, ObjectLayer layer)
        {
            DxfEllipse = dxfEllipse;
            Entity = dxfEllipse;
            Factory = factory;
            DeviceContext = deviceContext;
            ResCache = resCache;
            Layer = layer;
            EntityCount = 1;

            UpdateGeometry();
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
        public override void UpdateGeometry()
        {
            if (DxfEllipse.IsFullEllipse)
            {
                Geometry = GetEllipseGeometry();

                Stopwatch stopwatch = Stopwatch.StartNew();
                stopwatch.Restart();
                GeometryRealization = new(DeviceContext, Geometry, 1.0f, 0.25f, HairlineStrokeStyle);
                stopwatch.Stop();
                Debug.WriteLine($"DrawingEllipse GeometryRealization: {stopwatch.ElapsedMilliseconds} ms");

                var bounds = Geometry.GetBounds();
                Bounds = new(bounds.Left, bounds.Top, Math.Abs(bounds.Right - bounds.Left), Math.Abs(bounds.Bottom - bounds.Top));
            }
            else
            {
                Geometry = GetArcGeometry();

                Stopwatch stopwatch = Stopwatch.StartNew();
                stopwatch.Restart();
                GeometryRealization = new(DeviceContext, Geometry, 1.0f, 0.25f, HairlineStrokeStyle);
                stopwatch.Stop();
                Debug.WriteLine($"DrawingEllipse GeometryRealization: {stopwatch.ElapsedMilliseconds} ms");

                var bounds = Geometry.GetBounds();
                Bounds = new(bounds.Left, bounds.Top, Math.Abs(bounds.Right - bounds.Left), Math.Abs(bounds.Bottom - bounds.Top));
            }
        }
        public Geometry GetArcGeometry()
        {
            // Start by getting start and end points using NetDxf ToPolyline2D method
            RawVector2 startPoint = new(
                (float)DxfEllipse.ToPolyline2D(2).Vertexes.First().Position.X,
                (float)DxfEllipse.ToPolyline2D(2).Vertexes.First().Position.Y);
            RawVector2 endPoint = new(
                (float)DxfEllipse.ToPolyline2D(2).Vertexes.Last().Position.X,
                (float)DxfEllipse.ToPolyline2D(2).Vertexes.Last().Position.Y);
            var radiusX = (float)(DxfEllipse.MajorAxis / 2);
            var radiusY = (float)(DxfEllipse.MinorAxis / 2);
            float rotation = (float)DxfEllipse.Rotation; // Rotation in degrees

            // Get sweep and find out if large arc 
            double sweep;
            if (DxfEllipse.EndAngle < DxfEllipse.StartAngle)
            {
                sweep = (360 + DxfEllipse.EndAngle) - DxfEllipse.StartAngle;
            }
            else
            {
                sweep = Math.Abs(DxfEllipse.EndAngle - DxfEllipse.StartAngle);
            }
            bool isLargeArc = sweep >= 180;

            PathGeometry pathGeometry = new(Factory);
            using (var sink = pathGeometry.Open())
            {
                sink.BeginFigure(startPoint, FigureBegin.Filled);

                ArcSegment arcSegment = new()
                {
                    Point = endPoint,
                    Size = new(radiusX, radiusY),
                    SweepDirection = SweepDirection.Clockwise,
                    RotationAngle = rotation,
                    ArcSize = isLargeArc ? ArcSize.Large : ArcSize.Small
                };

                sink.AddArc(arcSegment);
                sink.EndFigure(FigureEnd.Open);
                sink.Close();

                // Apply rotation if needed
                if (rotation != 0)
                {
                    Matrix matrix = new();
                    //matrix.RotateAt((float)rotation, centerPoint.X, centerPoint.Y);

                    // Apply rotation transformation if required
                    RawMatrix3x2 transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22, (float)matrix.OffsetX, (float)matrix.OffsetY);

                    return new TransformedGeometry(Factory, pathGeometry, transform);
                }
                else
                {
                    return pathGeometry;
                }
            }
        }
        public Geometry GetEllipseGeometry()
        {
            // Extract properties from the netDxf Ellipse
            var center = DxfEllipse.Center;
            double majorAxis = DxfEllipse.MajorAxis;
            double minorAxis = DxfEllipse.MinorAxis;
            double rotation = DxfEllipse.Rotation; // Rotation in degrees

            // Convert center coordinates and axes to SharpDX format
            var centerPoint = new RawVector2((float)center.X, (float)center.Y);
            var width = (float)(majorAxis);
            var height = (float)(minorAxis);

            Matrix matrix = new();
            matrix.RotateAt((float)rotation, centerPoint.X, centerPoint.Y);

            // Create the SharpDX Ellipse
            Ellipse ellipse = new(centerPoint, width / 2, height / 2);

            // Create EllipseGeometry
            var ellipseGeometry = new EllipseGeometry(Factory, ellipse);

            // Apply rotation if needed
            if (rotation != 0)
            {
                // Apply rotation transformation if required
                RawMatrix3x2 transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22, (float)matrix.OffsetX, (float)matrix.OffsetY);
                return new TransformedGeometry(Factory, ellipseGeometry, transform);
            }
            else
            {
                return ellipseGeometry;
            }
        }
        public override bool Hittest(RawVector2 p, float thickness)
        {
            return Geometry.StrokeContainsPoint(p, thickness);
        }
        #endregion
    }
}
