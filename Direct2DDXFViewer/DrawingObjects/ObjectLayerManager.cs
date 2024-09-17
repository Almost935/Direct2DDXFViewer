using netDxf.Units;
using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class ObjectLayerManager : IDisposable
    {
        #region Fields
        private bool _disposed = false;
        #endregion

        #region Properties
        public Dictionary<string, ObjectLayer> Layers { get; set; } = new();
        #endregion

        #region Methods
        public ObjectLayer GetLayer(string layerName)
        {
            if (Layers.TryGetValue(layerName, out ObjectLayer layer)) { return layer; }
            else
            {
                ObjectLayer objectLayer = new() { Name = layerName };
                Layers.Add(layerName, objectLayer);
                return objectLayer;
            }
        }
        public void DrawVisibleObjectsToDeviceContext(DeviceContext1 deviceContext, float thickness)
        {
            foreach (var layer in Layers.Values)
            {
                layer.DrawVisibleObjectsToDeviceContext(deviceContext, thickness);
            }
        }
        public void DrawVisibleObjectsToRenderTarget(RenderTarget renderTarget, float thickness)
        {
            foreach (var layer in Layers.Values)
            {
                layer.DrawVisibleObjectsToRenderTarget(renderTarget, thickness);
            }
        }

        public void DrawObjectsToDeviceContext(DeviceContext1 deviceContext, float thickness)
        {
            foreach (var layer in Layers.Values)
            {
                layer.DrawObjectsToDeviceContext(deviceContext, thickness);
            }
        }
        public void DrawObjectsToRenderTarget(RenderTarget renderTarget, float thickness)
        {
            foreach (var layer in Layers.Values)
            {
                layer.DrawObjectsToRenderTarget(renderTarget, thickness);
            }
        }

        public List<DrawingObject> GetDrawingObjectsinRect(Rect rect)
        {
            List<DrawingObject> drawingObjects = [];
            foreach (var layer in Layers.Values)
            {
                foreach (var obj in layer.DrawingObjects)
                {
                    if (obj.DrawingObjectIsInRect(rect))
                    {
                        drawingObjects.Add(obj);
                    }
                }
            }
            return drawingObjects;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Dispose managed resources
                if (Layers != null)
                {
                    foreach (var layer in Layers.Values)
                    {
                        layer?.Dispose();
                    }
                    Layers.Clear();
                }
            }

            // Free unmanaged resources if any

            _disposed = true;
        }

        ~ObjectLayerManager()
        {
            Dispose(false);
        }
        #endregion
    }
}
