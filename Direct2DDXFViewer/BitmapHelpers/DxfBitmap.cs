using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Direct2DControl;
using Direct2DDXFViewer.DrawingObjects;
using Direct2DDXFViewer.Helpers;
using netDxf.Entities;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX.WIC;

using Bitmap = SharpDX.Direct2D1.Bitmap;
using BitmapInterpolationMode = SharpDX.Direct2D1.BitmapInterpolationMode;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class DxfBitmap : IDisposable
    {
        #region Fields
        private readonly DeviceContext1 _deviceContext;
        private readonly Factory1 _factory;
        private readonly ObjectLayerManager _layerManager;
        private RawMatrix3x2 _transform;
        private readonly string _tempFileFolderPath;
        private string _filepath;
        private SharpDX.WIC.Bitmap _wicBitmap;
        private WicRenderTarget _wicRenderTarget;
        private BitmapRenderTarget _bitmapRenderTarget;
        private ImagingFactory _imagingFactory;
        private bool _disposed = false;
        private int _maxBitmapSize;
        private StrokeStyle1 _hairlineStrokeStyle;
        #endregion

        #region Properties
        public int ZoomStep { get; set; }
        public float Zoom { get; set; }
        public Size2F Size { get; set; }
        public Bitmap Bitmap { get; set; }
        public bool BitmapSaved { get; set; } = false;
        public Rect DestRect { get; set; }
        public Rect Extents { get; set; }
        public Dictionary<(byte r, byte g, byte b, byte a), Brush> Brushes { get; set; } = new();
        public bool IsDisposed => _disposed;
        #endregion

        #region Constructor
        public DxfBitmap(DeviceContext1 deviceContext, Factory1 factory, ObjectLayerManager layerManager, Rect destRect, Rect extents, RawMatrix3x2 extentsMatrix, int zoomStep, float zoom, string tempFileFolder, Size2F size, int maxBitmapSize)
        {
            _deviceContext = deviceContext;
            _factory = factory;
            _layerManager = layerManager;
            DestRect = destRect;
            Extents = extents;
            _transform = extentsMatrix;
            ZoomStep = zoomStep;
            Zoom = zoom;
            _tempFileFolderPath = tempFileFolder;
            Size = size;
            _maxBitmapSize = maxBitmapSize;

            RenderBitmap();
            SaveBitmapToTemporaryFile();
        }
        #endregion

        #region Methods
        private void LoadBitmapFromFile()
        {
            if (BitmapSaved && (Bitmap == null || Bitmap.IsDisposed))
            {
                var loadedPixelData = File.ReadAllBytes(_filepath);
                BitmapData bitmapData = JsonSerializer.Deserialize<BitmapData>(File.ReadAllText(_filepath + ".meta"));
                var width = bitmapData.Width;
                var height = bitmapData.Height;
                var pixelFormat = bitmapData.PixelFormat;

                var bitmapProperties = new BitmapProperties(new SharpDX.Direct2D1.PixelFormat(pixelFormat, AlphaMode.Premultiplied));

                // Recreate the bitmap
                var recreatedBitmap = new Bitmap(_deviceContext, new Size2(width, height), bitmapProperties);

                // Ensure the stride is correctly calculated
                int stride = width * 4; // Assuming 4 bytes per pixel (BGRA format)
                recreatedBitmap.CopyFromMemory(loadedPixelData);

                Bitmap = recreatedBitmap;
            }
        }

        public void SaveBitmapToTemporaryFile()
        {
            if (!BitmapSaved)
            {
                Size2 size = Bitmap.PixelSize;
                int width = size.Width;
                int height = size.Height;
                var pixelFormat = Bitmap.PixelFormat.Format; // Use the original bitmap's pixel format
                var dataSize = width * height * 4; // Assuming 4 bytes per pixel (BGRA format)
                var pixelData = new byte[dataSize];

                var bitmapProperties = new BitmapProperties1
                {
                    BitmapOptions = BitmapOptions.CannotDraw | BitmapOptions.CpuRead,
                    PixelFormat = new SharpDX.Direct2D1.PixelFormat(pixelFormat, AlphaMode.Premultiplied)
                };
                Bitmap1 bitmap = new(_deviceContext, size, bitmapProperties);
                try
                {
                    bitmap.CopyFromBitmap(Bitmap);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error copying from bitmap: {ex.Message}");
                    throw;
                }
               
                var dataRect = bitmap.Map(MapOptions.Read);
                System.Runtime.InteropServices.Marshal.Copy(dataRect.DataPointer, pixelData, 0, dataSize);

                _filepath = Path.Combine(_tempFileFolderPath, $"{Guid.NewGuid()}.png");
                File.WriteAllBytes(_filepath, pixelData);

                // Save the pixel format and dimensions, if needed, in separate metadata for later recreation.
                BitmapData bitmapdata = new() { Width = width, Height = height, PixelFormat = pixelFormat };
                File.WriteAllText(_filepath + ".meta", JsonSerializer.Serialize(bitmapdata));

                BitmapSaved = true;
            }


            //if (!BitmapSaved)
            //{
            //    if (_imagingFactory == null || _wicBitmap == null || _tempFileFolderPath == null)
            //    {
            //        throw new InvalidOperationException("Necessary objects are not initialized.");
            //    }

            //    _filepath = Path.Combine(_tempFileFolderPath, $"{Guid.NewGuid()}.png");

            //    using (var stream = new WICStream(_imagingFactory, _filepath, SharpDX.IO.NativeFileAccess.Write))
            //    using (var encoder = new PngBitmapEncoder(_imagingFactory, stream))
            //    using (var frame = new BitmapFrameEncode(encoder))
            //    {
            //        frame.Initialize();
            //        frame.SetSize(_wicBitmap.Size.Width, _wicBitmap.Size.Height);
            //        var guid = SharpDX.WIC.PixelFormat.Format32bppPBGRA;
            //        frame.SetPixelFormat(ref guid);
            //        frame.WriteSource(_wicBitmap);
            //        frame.Commit();
            //        encoder.Commit();
            //    }
            //    _wicBitmap.Dispose();
            //    _imagingFactory.Dispose();

            //    BitmapSaved = true;
            //}
        }

        public bool BitmapInView(Rect view)
        {
            if (DestRect.IntersectsWith(view) || view.Contains(DestRect))
            {
                return true;
            }

            return false;
        }
        private void RenderBitmap()
        {
            _bitmapRenderTarget = new(_deviceContext, CompatibleRenderTargetOptions.None, Size)
            {
                DotsPerInch = new(96 * Zoom, 96 * Zoom),
                AntialiasMode = AntialiasMode.PerPrimitive
            };
            _bitmapRenderTarget.BeginDraw();
            _bitmapRenderTarget.Transform = _transform;
            _bitmapRenderTarget.Clear(new RawColor4(1.0f, 1.0f, 1.0f, 0));

            foreach (var layer in _layerManager.Layers.Values)
            {
                foreach (var drawingObject in layer.DrawingObjects)
                {
                    drawingObject.DrawToRenderTarget(_bitmapRenderTarget, drawingObject.Thickness, drawingObject.Brush, drawingObject.HairlineStrokeStyle);
                }
            }

            _bitmapRenderTarget.EndDraw();
            Bitmap = _bitmapRenderTarget.Bitmap;
            _bitmapRenderTarget.Dispose();


            //_imagingFactory = new();
            //_wicBitmap = new(_imagingFactory, (int)Size.Width, (int)Size.Height, SharpDX.WIC.PixelFormat.Format32bppPBGRA, BitmapCreateCacheOption.CacheOnLoad);
            //_wicRenderTarget = new(_factory, _wicBitmap, new RenderTargetProperties())
            //{
            //    DotsPerInch = new Size2F(96.0f * Zoom, 96.0f * Zoom),
            //    AntialiasMode = AntialiasMode.PerPrimitive
            //};
            //_wicRenderTarget.BeginDraw();
            //_wicRenderTarget.Transform = _transform;

            //foreach (var layer in _layerManager.Layers.Values)
            //{
            //    foreach (var drawingObject in layer.DrawingObjects)
            //    {
            //        drawingObject.DrawToRenderTarget(_wicRenderTarget, drawingObject.Thickness, GetDrawingObjectBrush(drawingObject.Entity, _wicRenderTarget), drawingObject.HairlineStrokeStyle);
            //    }
            //}
            //_wicRenderTarget.EndDraw();

            //Bitmap = Bitmap.FromWicBitmap(_deviceContext, _wicBitmap);
        }
        private Brush GetDrawingObjectBrush(EntityObject entity, WicRenderTarget target)
        {
            byte r, g, b, a;
            if (entity.Color.IsByLayer)
            {
                if (entity.Layer.Color.R == 255 && entity.Layer.Color.G == 255 && entity.Layer.Color.B == 255)
                {
                    r = g = b = 0; a = 255;
                }
                else
                {
                    r = entity.Layer.Color.R; g = entity.Layer.Color.G; b = entity.Layer.Color.B; a = 255;
                }
            }
            else
            {
                if (entity.Color.R == 255 && entity.Color.G == 255 && entity.Color.B == 255)
                {
                    r = g = b = 0; a = 255;
                }
                else
                {
                    r = entity.Color.R; g = entity.Color.G; b = entity.Color.B; a = 255;
                }
            }

            bool brushExists = Brushes.TryGetValue((r, g, b, a), out Brush brush);
            if (!brushExists)
            {
                brush = new SolidColorBrush(target, new RawColor4((float)r / 255, (float)g / 255, (float)b / 255, 1.0f));
                Brushes.Add((r, g, b, a), brush);
            }

            return brush;
        }

        public Bitmap GetBitmap()
        {
            if (Bitmap.IsDisposed)
            {
                LoadBitmapFromFile();
            }

            return Bitmap;
        }
        public void DisposeBitmap()
        {
            if (!BitmapSaved) { SaveBitmapToTemporaryFile(); }

            Bitmap?.Dispose();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _wicBitmap?.Dispose();
                    _wicRenderTarget?.Dispose();
                    _imagingFactory?.Dispose();
                    Bitmap?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DxfBitmap()
        {
            Dispose(false);
        }
        #endregion
    }

    public class BitmapData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public SharpDX.DXGI.Format PixelFormat { get; set; }
    }
}
