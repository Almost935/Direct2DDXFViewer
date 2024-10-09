using Direct2DControl;
using Direct2DDXFViewer.Helpers;
using netDxf.Entities;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class DrawingBlock : DrawingObject
    {
        #region Fields
        private Insert _dxfBlock;
        #endregion

        #region Properties
        public Insert DxfBlock
        {
            get { return _dxfBlock; }
            set
            {
                _dxfBlock = value;
                OnPropertyChanged(nameof(DxfBlock));
            }
        }
        public ObservableCollection<DrawingObject> DrawingObjects { get; set; } = new();


        public float CurrentScale { get; set; } = 1;
        #endregion

        #region Constructor
        public DrawingBlock(Insert dxfBlock, Factory1 factory, DeviceContext1 deviceContext, ResourceCache resCache, ObjectLayer layer)
        {
            DxfBlock = dxfBlock;
            Entity = dxfBlock;
            Factory = factory;
            DeviceContext = deviceContext;
            ResCache = resCache;
            Layer = layer;

            GetStrokeStyle();
            UpdateBrush();
        }
        #endregion

        #region Methods
        public void GetDrawingObjects()
        {
            foreach (var e in DxfBlock.Explode())
            {
                var obj = DxfHelpers.GetDrawingObject(e, Layer, Factory, DeviceContext, ResCache);

                if (obj is not null)
                {
                    EntityCount += obj.EntityCount;
                    DrawingObjects.Add(obj);
                }
            }
        }


        public override void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness, Brush brush)
        {
            foreach (var obj in DrawingObjects)
            {
                obj.DrawToDeviceContext(deviceContext, thickness, brush);
            }
        }
        public override void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness, Brush brush, StrokeStyle1 strokeStyle)
        {
            foreach (var obj in DrawingObjects)
            {
                obj.DrawToDeviceContext(deviceContext, thickness, brush, strokeStyle);
            }
        }
        public override void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush)
        {
            foreach (var obj in DrawingObjects)
            {
                obj.DrawToRenderTarget(target, thickness, brush);
            }
        }
        public override void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush, StrokeStyle1 strokeStyle)
        {
            foreach (var obj in DrawingObjects)
            {
                obj.DrawToRenderTarget(target, thickness, brush, strokeStyle);
            }
        }
        public override bool DrawingObjectIsInRect(Rect rect)
        {
            foreach (var obj in DrawingObjects)
            {
                if (obj.DrawingObjectIsInRect(rect))
                {
                    return true;
                }
            }
            return false;
        }

        public override async Task UpdateGeometriesAsync()
        {
            await Task.Run(() => InitializeGeometries());
        }
        public override void InitializeGeometries()
        {
            foreach (var obj in DrawingObjects)
            {
                obj.InitializeGeometries();

                if (Bounds.IsEmpty)
                {
                    Bounds = obj.Bounds;
                }
                else
                {
                    Bounds.Union(obj.Bounds);
                }
            }
        }
        public override List<GeometryRealization> GetGeometryRealization(float thickness)
        {
            List<GeometryRealization> geometryRealizations = [];

            foreach (var obj in DrawingObjects)
            {
                geometryRealizations.AddRange(obj.GetGeometryRealization(thickness));
            }

            return geometryRealizations;
        }
        public override bool Hittest(RawVector2 p, float thickness)
        {
            foreach (var obj in DrawingObjects)
            {
                if (obj.Bounds.Contains((double)p.X, (double)p.Y))
                {
                   if (obj.Hittest(p, thickness))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion
    }
}
