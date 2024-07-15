using Direct2DControl;
using Direct2DDXFViewer.DrawingObjects;
using Direct2DDXFViewer.Helpers;
using netDxf.Entities;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct2D1.Effects;
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
        public readonly RenderTarget _renderTarget;

        private readonly Factory1 _factory;
        private bool _disposed = false;
        private float highlightedThickness = 2.0f;
        private float snappedOuterThickness = 7.0f;
        private float highlightedOuterThickness = 9.0f;
        #endregion

        #region Properties
        public ZoomBitmap InitialZoomBitmap { get; set; }
        public ZoomBitmap CurrentZoomBitmap { get; set; }
        public Dictionary<double, ZoomBitmap> ZoomBitmaps { get; set; } = new();
        public Rect Extents { get; set; }
        public Matrix ExtentsMatrix { get; set; }
        public ObjectLayerManager LayerManager { get; set; } = new();
        public Size2F RenderTargetSize { get; set; }
        public ResourceCache ResCache { get; set; }
        public ZoomBitmap MaxZoomBitmap { get; set; }
        public ZoomBitmap InteractiveZoomBitmap { get; set; }
        public DrawingObject SnappedObject { get; set; }
        public List<DrawingObject> HighlightedObjects { get; set; } = new();

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
                InitialZoomBitmap?.Dispose();
                CurrentZoomBitmap?.Dispose();
                MaxZoomBitmap?.Dispose();

                if (ZoomBitmaps != null)
                {
                    foreach (var bitmapTup in ZoomBitmaps.Values)
                    {
                        bitmapTup?.Dispose();
                    }
                    ZoomBitmaps.Clear();
                }
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
                MaxZoomBitmap?.Dispose();

                Size2F maxSize = new
                    (
                    ResCache.MaxBitmapSize,
                    ResCache.MaxBitmapSize * (RenderTargetSize.Height / RenderTargetSize.Width)
                    );
                float maxZoom = maxSize.Height / RenderTargetSize.Height;
                Size2F maxDpi = new(96.0f * maxZoom, 96.0f * maxZoom);
                RawRectangleF maxRect = new(0, 0, RenderTargetSize.Width * maxZoom, RenderTargetSize.Height * maxZoom);

                MaxZoomBitmap = new()
                {
                    BitmapRenderTarget = new BitmapRenderTarget(_renderTarget, CompatibleRenderTargetOptions.None, maxSize)
                    {
                        DotsPerInch = maxDpi,
                        AntialiasMode = AntialiasMode.PerPrimitive,
                    },
                    Size = maxSize,
                    Zoom = maxZoom,
                    Dpi = maxDpi,
                    Rect = maxRect,
                };
                DrawDxfBitmapObjects(MaxZoomBitmap.BitmapRenderTarget, 1.0f);
            }
            else
            {
                MaxZoomBitmap?.Dispose();

                Size2F maxSize = new
                    (
                    ResCache.MaxBitmapSize * (RenderTargetSize.Width / RenderTargetSize.Height),
                    ResCache.MaxBitmapSize
                    );
                float maxZoom = maxSize.Width / RenderTargetSize.Width;
                Size2F maxDpi = new(96.0f * maxZoom, 96.0f * maxZoom);
                RawRectangleF maxRect = new(0, 0, RenderTargetSize.Width * maxZoom, RenderTargetSize.Height * maxZoom);

                MaxZoomBitmap = new()
                {
                    BitmapRenderTarget = new BitmapRenderTarget(_renderTarget, CompatibleRenderTargetOptions.None, maxSize)
                    {
                        DotsPerInch = maxDpi,
                        AntialiasMode = AntialiasMode.PerPrimitive,
                    },
                    Size = maxSize,
                    Zoom = maxZoom,
                    Dpi = maxDpi,
                    Rect = maxRect,
                };
                DrawDxfBitmapObjects(MaxZoomBitmap.BitmapRenderTarget, 1.0f);
            }
        }

        private void InitializeBitmap()
        {
            InitialZoomBitmap = GetZoomedBitmap(1.0f);
            CurrentZoomBitmap = InitialZoomBitmap;
        }
        public void UpdateCurrentBitmap(float zoom)
        {
            if (zoom == 1.0f)
            {
                CurrentZoomBitmap = InitialZoomBitmap;
            }
            else
            {
                CurrentZoomBitmap = GetZoomedBitmap(zoom);
            }
        }
        public ZoomBitmap GetZoomedBitmap(float zoom)
        {
            if (ZoomBitmaps.TryGetValue(Math.Round((double)zoom, 3), out ZoomBitmap zoomBitmap))
            {
                return zoomBitmap;
            }

            Size2F size = new(RenderTargetSize.Width * zoom, RenderTargetSize.Height * zoom);

            if (size.Width > ResCache.MaxBitmapSize ||
                size.Height > ResCache.MaxBitmapSize)
            {
                return MaxZoomBitmap;
            }

            zoomBitmap = new();
            zoomBitmap.Zoom = zoom;
            zoomBitmap.Size = size;
            zoomBitmap.Dpi = new(96 * zoom, 96 * zoom);
            zoomBitmap.Rect = new(0, 0, RenderTargetSize.Width * zoom, RenderTargetSize.Height * zoom);
            zoomBitmap.BitmapRenderTarget = new BitmapRenderTarget(_renderTarget, CompatibleRenderTargetOptions.None, size)
            {
                DotsPerInch = zoomBitmap.Dpi,
                AntialiasMode = AntialiasMode.PerPrimitive,
            };
            DrawDxfBitmapObjects(zoomBitmap.BitmapRenderTarget, zoom);
            ZoomBitmaps.Add(Math.Round((double)zoom, 3), zoomBitmap);

            return zoomBitmap;
        }
        public void DrawDxfBitmapObjects(BitmapRenderTarget bitmapRenderTarget, float zoom)
        {
            if (LayerManager is not null)
            {
                bitmapRenderTarget.BeginDraw();
                bitmapRenderTarget.Clear(new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));

                bitmapRenderTarget.Transform = new RawMatrix3x2((float)ExtentsMatrix.M11, (float)ExtentsMatrix.M12, (float)ExtentsMatrix.M21, (float)ExtentsMatrix.M22, (float)ExtentsMatrix.OffsetX, (float)ExtentsMatrix.OffsetY);

                float strokeThickness = 1.0f / (bitmapRenderTarget.Transform.M11 * zoom);

                foreach (var layer in LayerManager.Layers.Values)
                {
                    if (layer.IsVisible)
                    {
                        foreach (var o in layer.DrawingObjects)
                        {
                            o.Draw(bitmapRenderTarget, strokeThickness, o.Brush);
                        }
                    }
                }

                bitmapRenderTarget.EndDraw();
            }
        }
        public void DrawInteractiveBitmapObjects(BitmapRenderTarget bitmapRenderTarget, float zoom)
        {
            bitmapRenderTarget.BeginDraw();
            bitmapRenderTarget.Clear(new RawColor4(1.0f, 1.0f, 1.0f, 0.0f));

            bitmapRenderTarget.Transform = new RawMatrix3x2((float)ExtentsMatrix.M11, (float)ExtentsMatrix.M12, (float)ExtentsMatrix.M21, (float)ExtentsMatrix.M22, (float)ExtentsMatrix.OffsetX, (float)ExtentsMatrix.OffsetY);

            float highlightedStrokeThickness = highlightedThickness / (bitmapRenderTarget.Transform.M11 * zoom);
            float outerHighlightedStrokeThickness = highlightedOuterThickness / (bitmapRenderTarget.Transform.M11 * zoom);
            float outerSnappedStrokeThickness = snappedOuterThickness / (bitmapRenderTarget.Transform.M11 * zoom);

            foreach (var o in HighlightedObjects)
            {
                o.Draw(bitmapRenderTarget, highlightedStrokeThickness, ResCache.HighlightedBrush);
            }

            if (SnappedObject is not null)
            {
                if (SnappedObject.IsHighlighted)
                {
                    SnappedObject.Draw(bitmapRenderTarget, outerHighlightedStrokeThickness, ResCache.HighlightedOuterEdgeBrush);
                }
                else
                {
                    SnappedObject.Draw(bitmapRenderTarget, outerSnappedStrokeThickness, SnappedObject.OuterEdgeBrush);
                }
            }

            bitmapRenderTarget.EndDraw();
        }
        public void ZoomToExtents()
        {
            CurrentZoomBitmap = InitialZoomBitmap;
        }
        public void UpdateInteractiveObjects(DrawingObject snappedDrawingObject, List<DrawingObject> highlightedDrawingObjects)
        {
            if (CurrentZoomBitmap is null) { return; }

            if (InteractiveZoomBitmap is null)
            {
                InteractiveZoomBitmap = new();
            }

            InteractiveZoomBitmap.Zoom = CurrentZoomBitmap.Zoom;
            InteractiveZoomBitmap.Size = CurrentZoomBitmap.Size;
            InteractiveZoomBitmap.Dpi = CurrentZoomBitmap.Dpi;
            InteractiveZoomBitmap.Rect = CurrentZoomBitmap.Rect;
            InteractiveZoomBitmap.BitmapRenderTarget = new BitmapRenderTarget(_renderTarget, CompatibleRenderTargetOptions.None, CurrentZoomBitmap.Size)
            {
                DotsPerInch = CurrentZoomBitmap.Dpi,
                AntialiasMode = AntialiasMode.PerPrimitive,
            };

            SnappedObject = snappedDrawingObject;
            HighlightedObjects = highlightedDrawingObjects;

            DrawInteractiveBitmapObjects(InteractiveZoomBitmap.BitmapRenderTarget, CurrentZoomBitmap.Zoom);
        }
        #endregion
    }
}
