using Direct2DControl;
using netDxf.Entities;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class DrawingPolyline3D : DrawingPolyline
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
        public DrawingPolyline3D(Polyline3D dxfPolyline3D, Factory1 factory, DeviceContext1 deviceContext, ResourceCache resCache, ObjectLayer layer)
        {
            DxfPolyline3D = dxfPolyline3D;
            Entity = dxfPolyline3D;
            Factory = factory;
            DeviceContext = deviceContext;
            ResCache = resCache;
            Layer = layer;

            UpdateGeometry();
            GetStrokeStyle();
            UpdateBrush();
        }
        #endregion

        #region Methods
        public override void UpdateGeometry()
        {
            foreach (var e in DxfPolyline3D.Explode())
            {
                var obj = DxfHelpers.GetDrawingSegment(e, Layer, Factory, DeviceContext, ResCache);
                if (obj is not null) 
                {
                    EntityCount += obj.EntityCount;
                    DrawingSegments.Add(obj); 
                }
            }
        }
        #endregion
    }
}
