﻿using Direct2DControl;
using Direct2DDXFViewer.Helpers;
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
    public class DrawingPolyline2D : DrawingPolyline
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
        #endregion

        #region Constructor
        public DrawingPolyline2D(Polyline2D dxfPolyline2D, Factory1 factory, DeviceContext1 deviceContext, ResourceCache resCache, ObjectLayer layer)
        {
            DxfPolyline2D = dxfPolyline2D;
            Entity = dxfPolyline2D;
            Factory = factory;
            DeviceContext = deviceContext;
            ResCache = resCache;
            Layer = layer;

            GetStrokeStyle();
            UpdateBrush();
            GetDrawingSegments();
        }
        #endregion

        #region Methods
        public override void GetDrawingSegments()
        {
            foreach (var e in DxfPolyline2D.Explode())
            {
                var obj = DxfHelpers.GetDrawingSegment(e, Layer, Factory, DeviceContext, ResCache);
                if (obj is not null)
                {
                    EntityCount += obj.EntityCount;
                    DrawingSegments.Add(obj);
                }
            }
        }

        public override async Task UpdateGeometriesAsync()
        {
            await Task.Run(() => InitializeGeometries());
        }
        public override void InitializeGeometries()
        {
            //foreach (var segment in DrawingSegments)
            //{
            //    segment.UpdateGeometry();

            //    if (Bounds.IsEmpty)
            //    {
            //        Bounds = segment.Bounds;
            //    }
            //    else
            //    {
            //        Bounds = Rect.Union(Bounds, segment.Bounds);
            //    }
            //}

            Parallel.ForEach(DrawingSegments, segment =>
            {
                segment.InitializeGeometries();

                if (Bounds.IsEmpty)
                {
                    Bounds = segment.Bounds;
                }
                else
                {
                    Bounds = Rect.Union(Bounds, segment.Bounds);
                }
            });
        }
        #endregion
    }
}
