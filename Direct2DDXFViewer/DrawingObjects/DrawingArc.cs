﻿using Direct2DControl;
using netDxf.Entities;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

        public double Sweep { get; set; }
        public bool IsLargeArc { get; set; }
        #endregion

        #region Constructor
        public DrawingArc(Arc dxfArc, Factory1 factory, DeviceContext1 deviceContext, ResourceCache resCache, ObjectLayer layer)
        {
            DxfArc = dxfArc;
            Entity = dxfArc;
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
        public override async Task UpdateGeometriesAsync()
        {
            await Task.Run(() => InitializeGeometries());
        }
        public override void InitializeGeometries()
        {
            // Start by getting start and end points using NetDxf ToPolyline2D method
            StartPoint = new(
                (float)DxfArc.ToPolyline2D(2).Vertexes.First().Position.X,
                (float)DxfArc.ToPolyline2D(2).Vertexes.First().Position.Y);
            EndPoint = new(
                (float)DxfArc.ToPolyline2D(2).Vertexes.Last().Position.X,
                (float)DxfArc.ToPolyline2D(2).Vertexes.Last().Position.Y);

            // Get sweep and find out if large arc 
            if (DxfArc.EndAngle < DxfArc.StartAngle)
            {
                Sweep = (360 + DxfArc.EndAngle) - DxfArc.StartAngle;
            }
            else
            {
                Sweep = Math.Abs(DxfArc.EndAngle - DxfArc.StartAngle);
            }
            IsLargeArc = Sweep >= 180;

            PathGeometry pathGeometry = new(Factory);
            using (var sink = pathGeometry.Open())
            {
                sink.BeginFigure(StartPoint, FigureBegin.Filled);

                ArcSegment arcSegment = new()
                {
                    Point = EndPoint,
                    Size = new((float)DxfArc.Radius, (float)DxfArc.Radius),
                    SweepDirection = SweepDirection.Clockwise,
                    RotationAngle = (float)Sweep,
                    ArcSize = IsLargeArc ? ArcSize.Large : ArcSize.Small
                };

                sink.AddArc(arcSegment);
                sink.EndFigure(FigureEnd.Open);
                sink.Close();

                var simplifiedGeometry = new PathGeometry(Factory);

                // Open a GeometrySink to store the simplified version of the original geometry
                using (var geometrySink = simplifiedGeometry.Open())
                {
                    // Simplify the geometry, reducing it to line segments
                    pathGeometry.Simplify(GeometrySimplificationOption.CubicsAndLines, 0.25f, geometrySink);
                    geometrySink.Close();
                }
                Geometry = simplifiedGeometry;

                var bounds = Geometry.GetWidenedBounds(_hitTestStrokeThickness);
                Bounds = new(bounds.Left, bounds.Top, Math.Abs(bounds.Right - bounds.Left), Math.Abs(bounds.Bottom - bounds.Top));
            }
        }
        public override List<GeometryRealization> GetGeometryRealization(float thickness)
        {
            List<GeometryRealization> geometryRealizations = [];

            if (Geometry is not null)
            {
                geometryRealizations.Add(new(DeviceContext, Geometry, 1f, thickness, HairlineStrokeStyle));
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
