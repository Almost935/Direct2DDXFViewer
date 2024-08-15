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
        #endregion

        #region Properties
        public float Zoom { get; set; }
        public Bitmap Bitmap { get; set; }
        public bool BitmapLoaded => Bitmap != null;
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
            Debug.WriteLine($"66");
            if (!BitmapSaved)
            {
                ImagingFactory imagingFactory = new();
                Debug.WriteLine($"70");
                // Create a WIC Bitmap
                var wicBitmap = new SharpDX.WIC.Bitmap(imagingFactory, Bitmap.PixelSize.Width, Bitmap.PixelSize.Height,
                                            SharpDX.WIC.PixelFormat.Format32bppPBGRA, BitmapCreateCacheOption.CacheOnLoad);
                Debug.WriteLine($"74");
                // Create a RenderTarget for the WIC Bitmap
                using (var renderTarget = new WicRenderTarget(Bitmap.Factory, wicBitmap, new RenderTargetProperties()))
                {
                    // Copy the Direct2D Bitmap to the WIC Bitmap
                    renderTarget.BeginDraw();
                    renderTarget.DrawBitmap(Bitmap, 1.0f, SharpDX.Direct2D1.BitmapInterpolationMode.NearestNeighbor);
                    renderTarget.EndDraw();
                }
                Debug.WriteLine($"83");
                _filepath = Path.Combine(Path.GetTempPath(), $"{Zoom}.png");
                Debug.WriteLine($"85");
                // Select encoder based on file extension
                BitmapEncoder encoder;
                using (var stream = new WICStream(imagingFactory, _filepath, SharpDX.IO.NativeFileAccess.Write))
                {
                    encoder = new PngBitmapEncoder(imagingFactory, stream);
                    var frame = new BitmapFrameEncode(encoder);
                    frame.Initialize();
                    frame.SetSize(wicBitmap.Size.Width, wicBitmap.Size.Height);
                    var guid = SharpDX.WIC.PixelFormat.Format32bppPBGRA;
                    frame.SetPixelFormat(ref guid);
                    frame.WriteSource(wicBitmap);
                    frame.Commit();
                    encoder.Commit();
                }
                Debug.WriteLine($"100");
                wicBitmap.Dispose();
                imagingFactory.Dispose();

                BitmapSaved = true;
            }
        }

        private void RenderBitmap()
        {
            Debug.WriteLine($"108");
            Size2F size = new(_deviceContext.Size.Width * Zoom, _deviceContext.Size.Height * Zoom);
            BitmapRenderTarget bitmapRenderTarget = new(_deviceContext, CompatibleRenderTargetOptions.None, size)
            {
                DotsPerInch = new Size2F(96.0f * Zoom, 96.0f * Zoom),
                AntialiasMode = AntialiasMode.Aliased
            };

            bitmapRenderTarget.BeginDraw();
            bitmapRenderTarget.Transform = _extentsMatrix;
            _layerManager.DrawToRenderTarget(bitmapRenderTarget, 1);
            bitmapRenderTarget.EndDraw();

            Bitmap = bitmapRenderTarget.Bitmap;
            Debug.WriteLine($"122");
        }

        public Bitmap GetBitmap()
        {
            if (Bitmap is null)
            {
                LoadBitmapFromFile();
            }

            return Bitmap;
        }

        public void Dispose()
        {
            if (!BitmapSaved) { SaveBitmapToTemporaryFile(); }

            Bitmap?.Dispose();
        }
    }
    #endregion
}
