using Direct2DControl;
using Direct2DDXFViewer.DrawingObjects;
using Direct2DDXFViewer.Helpers;
using netDxf.Entities;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public (BitmapRenderTarget, RawRectangleF) InitialBitmapRenderTarget { get; set; }
        public (BitmapRenderTarget, RawRectangleF) CurrentBitmapTup { get; set; }
        public Dictionary<float, (BitmapRenderTarget, RawRectangleF)> BitmapRenderTargets { get; set; } = [];
        public Rect Extents { get; set; }
        public Matrix ExtentsMatrix { get; set; }
        public ObjectLayerManager LayerManager { get; set; } = new();
        public Size2F RenderTargetSize { get; set; }
        public ResourceCache ResCache { get; set; }
        public Size2F MaxSize { get; set; }
        public RawRectangleF MaxRect { get; set; }
        public float MaxZoom { get; set; }
        public Size2F MaxDpi { get; set; } 
        public float Zoom { get; set; } = 1.0f;
        public Size2F DPI { get; set; } = new(96.0f, 96.0f);
        #endregion

        #region Constructors
        public BitmapCache(RenderTarget renderTarget, Factory1 factory, ObjectLayerManager layerManager, Rect extents, Size2F renderTargetSize, Matrix extentsMatrix, ResourceCache resCache)
        {
            _renderTarget = renderTarget;
            LayerManager = layerManager;
            Extents = extents;
            _factory = factory;
            RenderTargetSize = renderTargetSize;
            ExtentsMatrix = extentsMatrix;
            ResCache = resCache;

            GetMaxValues();
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
            if (_disposed) { return; }

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

        public void GetMaxValues()
        {
            if (RenderTargetSize.Width > RenderTargetSize.Height)
            {
                MaxSize = new
                    (
                    ResCache.MaxBitmapSize,
                    ResCache.MaxBitmapSize * (RenderTargetSize.Height / RenderTargetSize.Width)
                    );
                MaxZoom = MaxSize.Height / RenderTargetSize.Height;
                MaxDpi = new(96.0f * MaxZoom, 96.0f * MaxZoom);
                MaxRect = new(0, 0, RenderTargetSize.Width * MaxZoom, RenderTargetSize.Height * MaxZoom);
            }
            else
            {
                MaxSize = new
                    (
                    ResCache.MaxBitmapSize * (RenderTargetSize.Width / RenderTargetSize.Height),
                    ResCache.MaxBitmapSize
                    );
                MaxZoom = MaxSize.Width / RenderTargetSize.Width;
                MaxDpi = new(96.0f * MaxZoom, 96.0f * MaxZoom);
                MaxRect = new(0, 0, RenderTargetSize.Width * MaxZoom, RenderTargetSize.Height * MaxZoom);
            }
        }

        public void InitializeBitmap()
        {
            InitialBitmapRenderTarget = GetZoomedBitmap(1.0f).bitmapRenderTarget;
            CurrentBitmapRenderTarget = InitialBitmapRenderTarget;
            CurrentBitmapSourceRect = new(0, 0, RenderTargetSize.Width * Zoom, RenderTargetSize.Height * Zoom);
        }
        public void UpdateCurrentBitmap(float zoom)
        {
            CurrentBitmapRenderTarget = GetZoomedBitmap(zoom).bitmapRenderTarget;
        }
        public (BitmapRenderTarget bitmapRenderTarget, RawRectangleF rect) GetZoomedBitmap(float zoom)
        {
            (BitmapRenderTarget bitmapRenderTarget, RawRectangleF rect) bitmapTup;
            if (BitmapRenderTargets.TryGetValue(zoom, out bitmapTup))
            {
                return bitmapTup;
            }

            Size2F size = new(RenderTargetSize.Width * zoom, RenderTargetSize.Height * zoom);

            if (size.Width > ResCache.MaxBitmapSize ||
                size.Height > ResCache.MaxBitmapSize)
            {
                bitmapTup.bitmapRenderTarget = new BitmapRenderTarget(_renderTarget, CompatibleRenderTargetOptions.None, MaxSize)
                {
                    DotsPerInch = MaxDpi,
                    AntialiasMode = AntialiasMode.PerPrimitive,
                };
                bitmapTup.rect = MaxRect;
                DrawBitmapObjects(bitmapTup.bitmapRenderTarget);
                BitmapRenderTargets.Add(zoom, bitmapTup);

                Debug.WriteLine($"Bitmap too large. Zoom: {zoom}. Size: {size}");

                return bitmapTup;
            }    

            DPI = new( 96 * zoom, 96 * zoom);
            bitmapTup.bitmapRenderTarget = new BitmapRenderTarget(_renderTarget, CompatibleRenderTargetOptions.None, size)
            {
                DotsPerInch = DPI,
                AntialiasMode = AntialiasMode.PerPrimitive,
            };
            bitmapTup.rect = new(0, 0, RenderTargetSize.Width * zoom, RenderTargetSize.Height * zoom);
            DrawBitmapObjects(bitmapTup.bitmapRenderTarget);
            BitmapRenderTargets.Add(zoom, bitmapTup);

            return bitmapTup;
        }
        public void DrawBitmapObjects(BitmapRenderTarget bitmapRenderTarget)
        {
            if (LayerManager is not null)
            {
                bitmapRenderTarget.BeginDraw();
                bitmapRenderTarget.Clear(new RawColor4(1.0f, 1.0f, 0.0f, 1.0f));

                bitmapRenderTarget.Transform = new RawMatrix3x2((float)ExtentsMatrix.M11, (float)ExtentsMatrix.M12, (float)ExtentsMatrix.M21, (float)ExtentsMatrix.M22, (float)ExtentsMatrix.OffsetX, (float)ExtentsMatrix.OffsetY);
                foreach (var layer in LayerManager.Layers.Values)
                {
                    if (layer.IsVisible)
                    {
                        foreach (var o in layer.DrawingObjects)
                        {
                            if (o is DrawingLine drawingLine)
                            {
                                bitmapRenderTarget.DrawLine(drawingLine.StartPoint, drawingLine.EndPoint, drawingLine.Brush, 1.0f, drawingLine.StrokeStyle);
                            }
                        }
                    }
                }

                bitmapRenderTarget.EndDraw();
            }
        }
        #endregion
    }
}
