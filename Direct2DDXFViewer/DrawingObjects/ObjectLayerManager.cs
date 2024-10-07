using Direct2DControl;
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
        private DeviceContext1 _deviceContext;
        private ResourceCache _resourceCache;
        #endregion

        #region Properties
        public Dictionary<string, ObjectLayer> Layers { get; set; } = new();
        public List<DrawingObject> DrawingObjects => Layers.Values.SelectMany(layer => layer.DrawingObjects).ToList();
        #endregion

        #region Constructors
        public ObjectLayerManager(DeviceContext1 deviceContext, ResourceCache resCache)
        {
            _deviceContext = deviceContext;
            _resourceCache = resCache;
        }
        #endregion

        #region Methods
        public ObjectLayer GetLayer(netDxf.Tables.Layer dxfLayer)
        {
            if (Layers.TryGetValue(dxfLayer.Name, out ObjectLayer layer)) { return layer; }
            else
            {
                var brush = _resourceCache.GetBrush(dxfLayer.Color.R, dxfLayer.Color.G, dxfLayer.Color.B, 255);
                ObjectLayer objectLayer = new(_deviceContext, dxfLayer.Name, brush);
                Layers.Add(dxfLayer.Name, objectLayer);
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

        public void LoadGeometryRealizations()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            //await Task.Run(() =>
            //{

            Parallel.ForEach(Layers.Values, layer =>
            {
                layer.LoadDrawingRealizations();
            });
            //foreach (var layer in Layers.Values)
            //{
            //    layer.LoadDrawingRealizations();
            //}
            //});

            stopwatch.Stop();
            Debug.WriteLine($"\nLoadGeometryRealizations Time: {stopwatch.ElapsedMilliseconds}");
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
