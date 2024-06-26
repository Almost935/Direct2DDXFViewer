﻿using netDxf.Entities;
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
    public class DrawingArc : DrawingObject
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

        RenderTarget Target { get; set; }
        #endregion

        #region Constructor
        public DrawingArc(Arc dxfArc, Factory factory, RenderTarget renderTarget)
        {
            DxfArc = dxfArc;
            Factory = factory; 

            UpdateBrush(dxfArc, renderTarget);
        }
        #endregion

        #region Methods
        public override void UpdateGeometry()
        {
            // Start by getting start and end points using NetDxf ToPolyline2D method
            RawVector2 start = new(
                (float)DxfArc.ToPolyline2D(2).Vertexes.First().Position.X,
                (float)DxfArc.ToPolyline2D(2).Vertexes.First().Position.Y);
            RawVector2 end = new(
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
                sink.BeginFigure(start, FigureBegin.Filled);

                ArcSegment arcSegment = new()
                {
                    Point = end,
                    Size = new((float)DxfArc.Radius, (float)DxfArc.Radius),
                    SweepDirection = SharpDX.Direct2D1.SweepDirection.CounterClockwise,
                    RotationAngle = (float)sweep,
                    ArcSize = isLargeArc ? ArcSize.Large : ArcSize.Small
                };
                sink.AddArc(arcSegment);
                sink.EndFigure(FigureEnd.Open);
                sink.Close();

                Geometry = pathGeometry;
            }
        }
        #endregion
    }
}