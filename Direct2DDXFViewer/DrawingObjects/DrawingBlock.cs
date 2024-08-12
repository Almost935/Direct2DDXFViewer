using Direct2DControl;
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
        private Line _dxfLine;
        #endregion

        #region Properties
        public Insert DxfBlock { get; set; }
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

            UpdateGeometry();
            GetStrokeStyle();
            GetThickness();
            UpdateBrush();
        }
        #endregion

        #region Methods
        public override void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness, Brush brush)
        {
            foreach (var obj in DrawingObjects)
            {
                obj.DrawToDeviceContext(deviceContext, thickness, brush);
            }
        }
        public override void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush)
        {
            foreach (var obj in DrawingObjects)
            {
                obj.DrawToRenderTarget(target, thickness, brush);
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
        public override void UpdateGeometry()
        {
            foreach (var e in DxfBlock.Explode())
            {
                var obj = DxfHelpers.GetDrawingObject(e, Layer, Factory, DeviceContext, ResCache);
                if (obj is not null) { DrawingObjects.Add(obj); }
            }
        }
        #endregion
    }
}
