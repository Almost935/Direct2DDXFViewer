using Direct2DDXFViewer.DrawingObjects;
using Direct2DDXFViewer.Helpers;
using netDxf.Entities;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using SolidColorBrush = SharpDX.Direct2D1.SolidColorBrush;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class BitmapCache : IDisposable
    {
        #region Fields
        private readonly RenderTarget _renderTarget;
        private readonly Factory1 _factory;
        private bool _disposed = false;
        #endregion

        #region Properties
        public BitmapRenderTarget InitialBitmapRenderTarget { get; set; }
        public BitmapRenderTarget CurrentBitmapRenderTarget { get; set; }
        public Rect Extents { get; set; }
        public Matrix ExtentsMatrix { get; set; }
        public ObjectLayerManager LayerManager { get; set; } = new();
        public Size2F RenderTargetSize { get; set; }
        public float Scale { get; set; } = 1.0f;
        #endregion

        #region Constructors
        public BitmapCache(RenderTarget renderTarget, Factory1 factory, ObjectLayerManager layerManager, Rect extents, Size2F renderTargetSize, Matrix extentsMatrix)
        {
            _renderTarget = renderTarget;
            LayerManager = layerManager;
            Extents = extents;
            _factory = factory;
            RenderTargetSize = renderTargetSize;
            ExtentsMatrix = extentsMatrix;

            InitializeBitmap();
        }
        #endregion

        #region Methods
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // Prevent finalizer from running
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed){ return; }

            if (disposing)
            {
                // Dispose managed state (managed objects).
                InitialBitmapRenderTarget?.Dispose();
            }

            // Free unmanaged resources (unmanaged objects) and override finalizer
            // Set large fields to null.
            _disposed = true;
        }

        // Override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        ~BitmapCache()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void InitializeBitmap()
        {
            Size2F size = new(RenderTargetSize.Width * 10, RenderTargetSize.Height * 10);
            InitialBitmapRenderTarget = new BitmapRenderTarget(_renderTarget, CompatibleRenderTargetOptions.None, size);

            DrawBitmapObjects();
        }
        public BitmapRenderTarget GetZoomedBitmap(float zoom)
        {
            BitmapRenderTarget bitmapRenderTarget = new(_renderTarget, CompatibleRenderTargetOptions.None, RenderTargetSize);

            return bitmapRenderTarget;
        }
        public void DrawBitmapObjects()
        {
            if (LayerManager is not null)
            {
                InitialBitmapRenderTarget.BeginDraw();
                InitialBitmapRenderTarget.Clear(new RawColor4(1.0f, 1.0f, 0.0f, 1.0f));
                
                InitialBitmapRenderTarget.Transform = new RawMatrix3x2((float)ExtentsMatrix.M11, (float)ExtentsMatrix.M12, (float)ExtentsMatrix.M21, (float)ExtentsMatrix.M22, (float)ExtentsMatrix.OffsetX, (float)ExtentsMatrix.OffsetY);

                foreach (var layer in LayerManager.Layers.Values)
                {
                    if (layer.IsVisible)
                    {
                        foreach (var o in layer.DrawingObjects)
                        {
                            if (o is DrawingLine drawingLine)
                            {
                                InitialBitmapRenderTarget.DrawLine(drawingLine.StartPoint, drawingLine.EndPoint, drawingLine.Brush, 1.0f, drawingLine.StrokeStyle);
                            }
                        }
                    }
                }

                 InitialBitmapRenderTarget.EndDraw();
            }
        }
        #endregion
    }
}
