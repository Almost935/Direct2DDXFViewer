using System;
using System.Diagnostics;
using System.IO;
using Direct2DDXFViewer.DrawingObjects;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX.WIC;

using Bitmap = SharpDX.Direct2D1.Bitmap;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class DxfBitmap
    {
        #region Fields
        private DeviceContext1 _deviceContext;
        private Factory1 _factory;
        private ObjectLayerManager _layerManager;
        private RawMatrix3x2 _extentsMatrix;
        private string _filepath;
        private SharpDX.WIC.Bitmap _wicBitmap;
        private WicRenderTarget _wicRenderTarget;
        private ImagingFactory _imagingFactory;
        #endregion

        #region Properties
        public float Zoom { get; set; }
        public Bitmap Bitmap { get; set; }
        public bool BitmapSaved = false;
        #endregion

        #region Constructor
        public DxfBitmap(DeviceContext1 deviceContext, Factory1 factory, ObjectLayerManager layerManager, RawMatrix3x2 extentsMatrix, float zoom)
        {
            _deviceContext = deviceContext;
            _factory = factory;
            _layerManager = layerManager;
            _extentsMatrix = extentsMatrix;
            Zoom = zoom;

            RenderBitmap();
            SaveBitmapToTemporaryFile();
        }
        #endregion

        #region Methods
        private void LoadBitmapFromFile()
        {
            Debug.WriteLine($"Loading bitmap from file: _filepath: {_filepath}");
            if (BitmapSaved)
            {
                ImagingFactory imagingFactory = new();
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
                imagingFactory.Dispose();
            }
        }

        public void SaveBitmapToTemporaryFile()
        {
            if (!BitmapSaved)
            {
                _filepath = Path.Combine(Path.GetTempPath(), $"{Zoom}.png");
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
            _wicRenderTarget = new(_factory, _wicBitmap, new RenderTargetProperties())
            {
                DotsPerInch = new Size2F(96.0f * Zoom, 96.0f * Zoom),
                AntialiasMode = AntialiasMode.PerPrimitive
            };
            _wicRenderTarget.BeginDraw();
            _wicRenderTarget.Transform = _extentsMatrix;
            _layerManager.DrawToRenderTarget(_wicRenderTarget, 1);
            _wicRenderTarget.EndDraw();

            Bitmap = Bitmap.FromWicBitmap(_deviceContext, _wicBitmap);
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

        public void Dispose()
        {
            Debug.WriteLine($"DISPOSE: {Zoom}");
            if (!BitmapSaved) { SaveBitmapToTemporaryFile(); }

            Bitmap.Dispose();

            Debug.WriteLine($"Bitmap.IsDisposed: {Bitmap.IsDisposed}");
        }
    }
    #endregion
}
