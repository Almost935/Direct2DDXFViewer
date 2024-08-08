using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
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
        public void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness)
        {
            foreach (var layer in Layers.Values)
            {
                layer.DrawToDeviceContext(deviceContext, thickness);
            }
        }
        public void DrawToRenderTarget(RenderTarget renderTarget, float thickness, Rect view)
        {
            foreach (var layer in Layers.Values)
            {
                layer.DrawToRenderTarget(renderTarget, thickness, view);
            }
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
