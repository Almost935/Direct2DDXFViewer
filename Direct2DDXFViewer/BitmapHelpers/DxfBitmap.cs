using System;
using System.Diagnostics;
using System.IO;
using Direct2DDXFViewer.DrawingObjects;
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
        private DeviceContext1 _deviceContext;
        private Factory1 _factory;
        private ObjectLayerManager _layerManager;
        private RawMatrix3x2 _extentsMatrix;
        private string _tempFileFolderPath;
        private string _filepath;
        private SharpDX.WIC.Bitmap _wicBitmap;
        private WicRenderTarget _wicRenderTarget;
        private ImagingFactory _imagingFactory;
        private bool _disposed = false;
        #endregion

        #region Properties
        public float Zoom { get; set; }
        public Bitmap Bitmap { get; set; }
        public bool BitmapSaved = false;
        public Dictionary<(byte r, byte g, byte b, byte a), Brush> Brushes { get; set; } = new();

        #endregion

        #region Constructor
        public DxfBitmap(DeviceContext1 deviceContext, Factory1 factory, ObjectLayerManager layerManager, RawMatrix3x2 extentsMatrix, float zoom, string tempFileFolder)
        {
            _deviceContext = deviceContext;
            _factory = factory;
            _layerManager = layerManager;
            _extentsMatrix = extentsMatrix;
            Zoom = zoom;
            _tempFileFolderPath = tempFileFolder;

            RenderBitmap();
            SaveBitmapToTemporaryFile();
        }
        #endregion

        #region Methods
        private void LoadBitmapFromFile()
        {
            if (BitmapSaved)
            {
                while (IsFileInUse(_filepath))
                {
                    Thread.Sleep(500);
                }

                using (var imagingFactory = new ImagingFactory()) // Use 'using' to ensure disposal
                {
                    // Load the image using WIC
                    using (var decoder = new BitmapDecoder(imagingFactory, _filepath, DecodeOptions.CacheOnLoad))
                    using (var frame = decoder.GetFrame(0))
                    using (var converter = new FormatConverter(imagingFactory))
                    {
                        // Convert the image format to 32bppPBGRA which is compatible with Direct2D
                        converter.Initialize(frame, SharpDX.WIC.PixelFormat.Format32bppPBGRA);

                        // Create a Direct2D Bitmap from the WIC Bitmap
                        Bitmap = Bitmap.FromWicBitmap(_deviceContext, converter);
                    }
                }
            }
        }

        private bool IsFileInUse(string filePath)
        {
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    // If we can open the file with exclusive access, it's not in use
                    return false;
                }
            }
            catch (IOException)
            {
                // If an IOException is thrown, the file is in use
                return true;
            }
        }

        public void SaveBitmapToTemporaryFile()
        {
            if (!BitmapSaved)
            {
                _filepath = Path.Combine(_tempFileFolderPath, $"{Zoom}.png");
                // Select encoder based on file extension
                BitmapEncoder encoder;
                using (var stream = new WICStream(_imagingFactory, _filepath, SharpDX.IO.NativeFileAccess.Write))
                {
                    encoder = new PngBitmapEncoder(_imagingFactory, stream);
                    var frame = new BitmapFrameEncode(encoder);
                    frame.Initialize();
                    frame.SetSize(_wicBitmap.Size.Width, _wicBitmap.Size.Height);
                    var guid = SharpDX.WIC.PixelFormat.Format32bppPBGRA;
                    frame.SetPixelFormat(ref guid);
                    frame.WriteSource(_wicBitmap);
                    frame.Commit();
                    encoder.Commit();
                }
                _wicBitmap.Dispose();
                _imagingFactory.Dispose();

                BitmapSaved = true;
            }
        }

        private void RenderBitmap()
        {
            _imagingFactory = new();
            _wicBitmap = new SharpDX.WIC.Bitmap(_imagingFactory, (int)(_deviceContext.Size.Width * Zoom), (int)(_deviceContext.Size.Height * Zoom), SharpDX.WIC.PixelFormat.Format32bppPBGRA, BitmapCreateCacheOption.CacheOnLoad);

            Debug.WriteLine($"_wicBitmap.Size: {_wicBitmap.Size.Width} {_wicBitmap.Size.Height}");

            _wicRenderTarget = new(_factory, _wicBitmap, new RenderTargetProperties())
            {
                DotsPerInch = new Size2F(96.0f * Zoom, 96.0f * Zoom),
                AntialiasMode = AntialiasMode.PerPrimitive
            };
            _wicRenderTarget.BeginDraw();
            _wicRenderTarget.Transform = _extentsMatrix;

            foreach (var layer in _layerManager.Layers.Values)
            {
                foreach (var drawingObject in layer.DrawingObjects)
                {
                    drawingObject.DrawToRenderTarget(_wicRenderTarget, drawingObject.Thickness, GetDrawingObjectBrush(drawingObject.Entity, _wicRenderTarget));
                }
            }
            _wicRenderTarget.EndDraw();

            Bitmap = Bitmap.FromWicBitmap(_deviceContext, _wicBitmap);
        }
        public Brush GetDrawingObjectBrush(EntityObject entity, WicRenderTarget target)
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
            Debug.WriteLine($"GetBitmap: Zoom: {Zoom} Bitmap.IsDisposed: {Bitmap.IsDisposed}");
            if (Bitmap.IsDisposed)
            {
                LoadBitmapFromFile();
            }

            return Bitmap;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _wicBitmap?.Dispose();
                    _wicRenderTarget?.Dispose();
                    _imagingFactory?.Dispose();
                    Bitmap?.Dispose();
                }

                // Dispose unmanaged resources

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Debug.WriteLine($"DISPOSE: {Zoom}");
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DxfBitmap()
        {
            Dispose(false);
        }
    }
    #endregion
}
