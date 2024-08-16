using netDxf.Tables;
using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class ObjectLayer : INotifyPropertyChanged, IDisposable
    {
        #region Fields
        private string _name;
        private List<DrawingObject> _drawingObjects = new();
        private bool isVisible = true;
        private bool _disposed = false;
        #endregion

        #region Properties
        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        public List<DrawingObject> DrawingObjects
        {
            get { return _drawingObjects; }
            set
            {
                _drawingObjects = value;
                OnPropertyChanged(nameof(DrawingObjects));
            }
        }
        public bool IsVisible
        {
            get { return isVisible; }
            set
            {
                isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Methods
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness)
        {
            if (!IsVisible) { return; }

            foreach (var drawingObject in DrawingObjects)
            {
                if (drawingObject.IsInView)
                {
                    drawingObject.DrawToDeviceContext(deviceContext, thickness, drawingObject.Brush);
                }
            }
        }
        public void DrawToRenderTarget(RenderTarget renderTarget, float thickness)
        {
            if (!IsVisible) { return; }
            Brush brush = new SolidColorBrush(renderTarget, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 1));
            foreach (var drawingObject in DrawingObjects)
            {
                if (drawingObject.IsInView)
                {
                    drawingObject.DrawToRenderTarget(renderTarget, thickness, brush);
                }
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
                if (_drawingObjects != null)
                {
                    foreach (var drawingObject in _drawingObjects)
                    {
                        if (drawingObject is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    _drawingObjects.Clear();
                }
            }

            // Free unmanaged resources if any

            _disposed = true;
        }

        ~ObjectLayer()
        {
            Dispose(false);
        }
        #endregion
    }
}
